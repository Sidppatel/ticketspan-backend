using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;
using Stripe;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;

namespace TicketSpan.Api.Payments;







public sealed class StripeWebhookHandler
{
    private readonly Db db;
    private readonly ILogger<StripeWebhookHandler> logger;
    private readonly IEmailService emailService;
    private readonly EmailTemplateRenderer templates;
    private readonly AppSettingsProvider settings;

    public StripeWebhookHandler(
        Db db,
        ILogger<StripeWebhookHandler> logger,
        IEmailService emailService,
        EmailTemplateRenderer templates,
        AppSettingsProvider settings)
    {
        this.db = db;
        this.logger = logger;
        this.emailService = emailService;
        this.templates = templates;
        this.settings = settings;
    }

    public async Task HandleAsync(Event stripeEvent, CancellationToken ct)
    {
        await using var connection = await db.OpenBootstrapAsync(ct);

        switch (stripeEvent.Type)
        {
            case "payment_intent.succeeded":
                await OnPaymentSucceeded(connection, (PaymentIntent)stripeEvent.Data.Object, ct);
                break;

            case "payment_intent.payment_failed":
                
                await RunByIntent(connection, "sp_fail_booking_payment",
                    ((PaymentIntent)stripeEvent.Data.Object).Id, ct);
                break;

            case "payment_intent.canceled":
                await OnPaymentCanceled(connection, (PaymentIntent)stripeEvent.Data.Object, ct);
                break;

            case "payment_intent.processing":
                
                
                await RunByIntent(connection, "sp_mark_booking_processing",
                    ((PaymentIntent)stripeEvent.Data.Object).Id, ct);
                break;

            case "payment_intent.created":
            case "payment_intent.requires_action":
                
                break;

            case "charge.refunded":
                await OnChargeRefunded(connection, (Charge)stripeEvent.Data.Object, ct);
                break;

            case "charge.refund.updated":
                await OnRefundUpdated(connection, (Refund)stripeEvent.Data.Object, ct);
                break;

            case "charge.dispute.created":
                logger.LogWarning("Stripe dispute opened: {Dispute}", ((Dispute)stripeEvent.Data.Object).Id);
                break;

            case "account.updated":
                await OnAccountUpdated(connection, (Account)stripeEvent.Data.Object, ct);
                break;

            case "account.external_account.created":
            case "account.external_account.updated":
            case "account.external_account.deleted":
                break;

            case "transfer.created":
            case "transfer.reversed":
            case "transfer.updated":
                await OnTransfer(connection, (Transfer)stripeEvent.Data.Object, ct);
                break;

            case "payout.created":
            case "payout.updated":
            case "payout.paid":
            case "payout.failed":
            case "payout.canceled":
            case "payout.reconciliation_completed":
                await OnPayout(connection, stripeEvent, (Payout)stripeEvent.Data.Object, ct);
                break;

            default:
                logger.LogInformation("Unhandled Stripe event {Type}", stripeEvent.Type);
                break;
        }
    }

    private async Task OnPaymentSucceeded(NpgsqlConnection conn, PaymentIntent pi, CancellationToken ct)
    {
        await SetTxStatus(conn, pi.Id, "Succeeded", ct);

        
        var bookingId = await BookingIdForIntent(conn, pi, ct);
        if (bookingId is null)
        {
            logger.LogWarning("payment_intent.succeeded {Id} had no matching booking", pi.Id);
            return;
        }

        var qrToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        bool confirmed;
        await using (var cmd = new NpgsqlCommand("SELECT sp_confirm_booking(@b, @qr)", conn))
        {
            cmd.Parameters.AddWithValue("b", bookingId.Value);
            cmd.Parameters.AddWithValue("qr", qrToken);
            confirmed = await cmd.ExecuteScalarAsync(ct) is true;
        }

        var methodType = pi.LatestCharge?.PaymentMethodDetails?.Type;
        var methodLast4 = methodType switch
        {
            "card" => pi.LatestCharge?.PaymentMethodDetails?.Card?.Last4,
            "us_bank_account" => pi.LatestCharge?.PaymentMethodDetails?.UsBankAccount?.Last4,
            _ => null
        };

        await using (var enrich = new NpgsqlCommand(
            "SELECT sp_enrich_stripe_transaction(@id, @total, @fees, @mtype, @mlast4)", conn))
        {
            enrich.Parameters.AddWithValue("id", pi.Id);
            enrich.Parameters.AddWithValue("total", (int)pi.AmountReceived);
            enrich.Parameters.AddWithValue("fees", DBNull.Value);
            enrich.Parameters.AddWithValue("mtype", (object?)methodType ?? DBNull.Value);
            enrich.Parameters.AddWithValue("mlast4", (object?)methodLast4 ?? DBNull.Value);
            await enrich.ExecuteNonQueryAsync(ct);
        }

        if (confirmed)
        {
            await BookingEmailSender.SendBookingConfirmationEmailAsync(
                conn, bookingId.Value, emailService, templates, settings, logger, ct);
        }
    }

    private async Task OnPaymentCanceled(NpgsqlConnection conn, PaymentIntent pi, CancellationToken ct)
    {
        await SetTxStatus(conn, pi.Id, "Failed", ct);
        var bookingId = await BookingIdForIntent(conn, pi, ct);
        if (bookingId is { } id)
        {
            await using var cmd = new NpgsqlCommand("SELECT sp_cancel_booking(@b)", conn);
            cmd.Parameters.AddWithValue("b", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task OnChargeRefunded(NpgsqlConnection conn, Charge charge, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(charge.PaymentIntentId))
        {
            return;
        }
        await RefundBookingByIntent(conn, charge.PaymentIntentId, ct);
    }

    private async Task OnRefundUpdated(NpgsqlConnection conn, Refund refund, CancellationToken ct)
    {
        if (refund.Status != "succeeded" || string.IsNullOrEmpty(refund.PaymentIntentId))
        {
            return;
        }
        await RefundBookingByIntent(conn, refund.PaymentIntentId, ct);
    }

    private async Task RefundBookingByIntent(NpgsqlConnection conn, string intentId, CancellationToken ct)
    {
        Guid? bookingId;
        await using (var look = new NpgsqlCommand(
            "SELECT bookings_id FROM vw_stripe_transactions WHERE payment_intent_id = @id", conn))
        {
            look.Parameters.AddWithValue("id", intentId);
            bookingId = await look.ExecuteScalarAsync(ct) as Guid?;
        }
        if (bookingId is { } id)
        {
            await using var cmd = new NpgsqlCommand("SELECT sp_refund_booking(@b)", conn);
            cmd.Parameters.AddWithValue("b", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task OnAccountUpdated(NpgsqlConnection conn, Account account, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_tenant_stripe_status(@acct, @charges, @payouts, @details, @req)", conn);
        cmd.Parameters.AddWithValue("acct", account.Id);
        cmd.Parameters.AddWithValue("charges", account.ChargesEnabled);
        cmd.Parameters.AddWithValue("payouts", account.PayoutsEnabled);
        cmd.Parameters.AddWithValue("details", account.DetailsSubmitted);
        var reqJson = account.Requirements?.CurrentlyDue is { } due
            ? System.Text.Json.JsonSerializer.Serialize(due)
            : null;
        cmd.Parameters.Add(new NpgsqlParameter("req", NpgsqlDbType.Jsonb)
        {
            Value = (object?)reqJson ?? DBNull.Value
        });
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "no_data_found" || ex.SqlState == "P0002")
        {
            
            logger.LogInformation("account.updated for untracked account {Acct}", account.Id);
        }
    }

    private async Task OnTransfer(NpgsqlConnection conn, Transfer transfer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(transfer.DestinationId))
        {
            return;
        }
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_insert_stripe_transfer(@tid, @acct, @pi, @amt, @cur, @raw)", conn);
        cmd.Parameters.AddWithValue("tid", transfer.Id);
        cmd.Parameters.AddWithValue("acct", transfer.DestinationId);
        cmd.Parameters.AddWithValue("pi", DBNull.Value); 
        cmd.Parameters.AddWithValue("amt", (int)transfer.Amount);
        cmd.Parameters.AddWithValue("cur", transfer.Currency ?? "usd");
        cmd.Parameters.Add(new NpgsqlParameter("raw", NpgsqlDbType.Jsonb) { Value = transfer.ToJson() });
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "no_data_found")
        {
            logger.LogInformation("transfer for untracked account {Acct}", transfer.DestinationId);
        }
    }

    private async Task OnPayout(NpgsqlConnection conn, Event evt, Payout payout, CancellationToken ct)
    {
        
        var accountId = evt.Account;
        if (string.IsNullOrEmpty(accountId))
        {
            return;
        }
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_stripe_payout(@pid, @acct, @amt, @cur, @status, @arrival, @paid, @raw)", conn);
        cmd.Parameters.AddWithValue("pid", payout.Id);
        cmd.Parameters.AddWithValue("acct", accountId);
        cmd.Parameters.AddWithValue("amt", (int)payout.Amount);
        cmd.Parameters.AddWithValue("cur", payout.Currency ?? "usd");
        cmd.Parameters.AddWithValue("status", payout.Status);
        cmd.Parameters.AddWithValue("arrival",
            (object?)(payout.ArrivalDate == default ? null : payout.ArrivalDate) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("paid",
            (object?)(payout.Status == "paid" ? DateTime.UtcNow : null) ?? DBNull.Value);
        cmd.Parameters.Add(new NpgsqlParameter("raw", NpgsqlDbType.Jsonb) { Value = payout.ToJson() });
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "no_data_found")
        {
            logger.LogInformation("payout for untracked account {Acct}", accountId);
        }
    }

    private static async Task RunByIntent(NpgsqlConnection conn, string sp, string intentId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand($"SELECT {sp}(@id)", conn);
        cmd.Parameters.AddWithValue("id", intentId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task SetTxStatus(NpgsqlConnection conn, string intentId, string status, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT sp_update_stripe_transaction_status(@id, @s)", conn);
        cmd.Parameters.AddWithValue("id", intentId);
        cmd.Parameters.AddWithValue("s", status);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid?> BookingIdForIntent(NpgsqlConnection conn, PaymentIntent pi, CancellationToken ct)
    {
        
        await using (var look = new NpgsqlCommand(
            "SELECT bookings_id FROM vw_stripe_transactions WHERE payment_intent_id = @id", conn))
        {
            look.Parameters.AddWithValue("id", pi.Id);
            if (await look.ExecuteScalarAsync(ct) is Guid g)
            {
                return g;
            }
        }
        if (pi.Metadata is not null && pi.Metadata.TryGetValue("bookings_id", out var raw) && Guid.TryParse(raw, out var parsed))
        {
            return parsed;
        }
        return null;
    }
}
