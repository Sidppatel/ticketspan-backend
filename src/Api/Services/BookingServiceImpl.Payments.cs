using Grpc.Core;
using Npgsql;
using Stripe;
using TicketSpan.Api.Data;
using TicketSpan.Api.Payments;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Booking;
using TicketSpan.Protos.Pricing;
using TicketSpan.Api.Email;

namespace TicketSpan.Api.Services;

public sealed partial class BookingServiceImpl
{
    public override async Task<PaymentIntentResponse> CreatePaymentIntent(PaymentIntentRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        if (!stripe.Configured)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Payments are not configured"));
        }
        if (!Guid.TryParse(request.BookingsId, out var bookingId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid booking id"));
        }

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        long subtotal, fee, total, tax;
        string currency, status, taxCollectionMode;
        string? connectedAccount, existingIntent;
        DateTime? holdExpiresAt;
        bool achAllowed;
        var metadata = new Dictionary<string, string>();
        try
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT status, subtotal_cents, fee_cents, total_cents, currency, connected_account_id, "
                + "charges_enabled, existing_intent_id, hold_expires_at, ach_allowed, "
                + "tax_cents, tax_rate, venue_zip, venue_city, venue_state, event_name, ticket_count, "
                + "tenant_name, event_date, tax_state_cents, tax_county_cents, tax_city_cents, tax_local_cents, tax_jurisdiction, "
                + "tax_collection_mode "
                + "FROM sp_get_booking_for_payment(@b, @u)", connection);
            cmd.Parameters.AddWithValue("b", bookingId);
            cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
            }
            status = reader.GetString(0);
            subtotal = reader.GetInt32(1);
            fee = reader.GetInt32(2);
            total = reader.GetInt32(3);
            currency = reader.GetString(4);
            connectedAccount = reader.IsDBNull(5) ? null : reader.GetString(5);
            var chargesEnabled = !reader.IsDBNull(6) && reader.GetBoolean(6);
            existingIntent = reader.IsDBNull(7) ? null : reader.GetString(7);
            holdExpiresAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8);
            achAllowed = !reader.IsDBNull(9) && reader.GetBoolean(9);
            tax = reader.IsDBNull(10) ? 0 : reader.GetInt32(10);
            taxCollectionMode = reader.IsDBNull(24) ? "platform" : reader.GetString(24);

            metadata["tenant_id"] = tenantContext.TenantsId?.ToString() ?? string.Empty;
            metadata["subtotal_cents"] = subtotal.ToString();
            metadata["service_fee_cents"] = (fee - tax).ToString();
            metadata["tax_cents"] = tax.ToString();
            metadata["tax_rate"] = reader.IsDBNull(11) ? "0" : reader.GetDecimal(11).ToString(System.Globalization.CultureInfo.InvariantCulture);
            metadata["total_cents"] = total.ToString();
            metadata["venue_zip"] = reader.IsDBNull(12) ? string.Empty : reader.GetString(12);
            metadata["venue_city"] = reader.IsDBNull(13) ? string.Empty : reader.GetString(13);
            metadata["venue_state"] = reader.IsDBNull(14) ? string.Empty : reader.GetString(14);
            metadata["event_name"] = reader.IsDBNull(15) ? string.Empty : reader.GetString(15);
            metadata["ticket_count"] = (reader.IsDBNull(16) ? 1 : reader.GetInt32(16)).ToString();
            metadata["tenant_name"] = reader.IsDBNull(17) ? string.Empty : reader.GetString(17);
            metadata["event_date"] = reader.IsDBNull(18) ? string.Empty : reader.GetDateTime(18).ToString("yyyy-MM-dd");
            metadata["tax_state_cents"] = (reader.IsDBNull(19) ? 0 : reader.GetInt32(19)).ToString();
            metadata["tax_county_cents"] = (reader.IsDBNull(20) ? 0 : reader.GetInt32(20)).ToString();
            metadata["tax_city_cents"] = (reader.IsDBNull(21) ? 0 : reader.GetInt32(21)).ToString();
            metadata["tax_local_cents"] = (reader.IsDBNull(22) ? 0 : reader.GetInt32(22)).ToString();
            metadata["tax_jurisdiction"] = reader.IsDBNull(23) ? string.Empty : reader.GetString(23);
            metadata["tax_collected_by"] = taxCollectionMode;
            metadata["payment_purpose"] = "ticket_purchase";

            if (string.IsNullOrEmpty(connectedAccount) || !chargesEnabled)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    "Seller is not yet able to accept payments"));
            }
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }

        var preferAch = request.PreferredMethod == "ach" && achAllowed;
        if (preferAch)
        {
            try
            {
                await using var rp = new NpgsqlCommand(
                    "SELECT total_cents, fee_cents, tax_cents FROM sp_reprice_booking_for_method(@b, @u, 'ach')", connection);
                rp.Parameters.AddWithValue("b", bookingId);
                rp.Parameters.AddWithValue("u", tenantContext.UsersId!);
                await using var rr = await rp.ExecuteReaderAsync(ct);
                if (await rr.ReadAsync(ct))
                {
                    total = rr.GetInt32(0);
                    fee = rr.GetInt32(1);
                    tax = rr.GetInt32(2);
                    metadata["tax_cents"] = tax.ToString();
                    metadata["service_fee_cents"] = (fee - tax).ToString();
                    metadata["total_cents"] = total.ToString();
                }
            }
            catch (PostgresException ex)
            {
                throw MapPostgres(ex);
            }
        }

        var applicationFee = taxCollectionMode == "self" ? fee - tax : fee;

        string? stripeCustomerId = null;
        if (tenantContext.UsersId is not null)
        {
            string userEmail = "";
            string userName = "";
            await using (var uCmd = new NpgsqlCommand(
                "SELECT email, first_name || ' ' || last_name, stripe_customer_id FROM vw_user_profile WHERE users_id = @u", connection))
            {
                uCmd.Parameters.AddWithValue("u", tenantContext.UsersId.Value);
                await using var uReader = await uCmd.ExecuteReaderAsync(ct);
                if (await uReader.ReadAsync(ct))
                {
                    userEmail = uReader.GetString(0);
                    userName = uReader.GetString(1);
                    stripeCustomerId = uReader.IsDBNull(2) ? null : uReader.GetString(2);
                }
            }

            if (string.IsNullOrEmpty(stripeCustomerId) && !string.IsNullOrEmpty(userEmail))
            {
                stripeCustomerId = await stripe.GetOrCreateCustomerAsync(null, userEmail, userName, tenantContext.UsersId.Value, ct);
                await using var saveCustCmd = new NpgsqlCommand(
                    "SELECT sp_get_or_set_user_stripe_customer(@u, @c)", connection);
                saveCustCmd.Parameters.AddWithValue("u", tenantContext.UsersId.Value);
                saveCustCmd.Parameters.AddWithValue("c", stripeCustomerId);
                await saveCustCmd.ExecuteScalarAsync(ct);
            }
        }

        Stripe.PaymentIntent intent;
        try
        {
            if (!string.IsNullOrEmpty(existingIntent))
            {
                var existing = await stripe.GetPaymentIntentAsync(existingIntent, ct);
                if (existing.Status == "succeeded" || existing.Status == "processing")
                {
                    intent = existing;
                }
                else if (!preferAch && StripeService.IsPayable(existing.Status))
                {
                    intent = existing;
                }
                else
                {
                    intent = await stripe.CreateDestinationPaymentIntentAsync(
                        total, applicationFee, currency, connectedAccount!, bookingId, achAllowed, preferAch, ct, metadata, stripeCustomerId);
                }
            }
            else
            {
                intent = await stripe.CreateDestinationPaymentIntentAsync(
                    total, applicationFee, currency, connectedAccount!, bookingId, achAllowed, preferAch, ct, metadata, stripeCustomerId);
            }
        }
        catch (StripeException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, $"Stripe payment setup failed: {ex.StripeError?.Message ?? ex.Message}"));
        }

        string? customerSessionClientSecret = null;
        if (!string.IsNullOrEmpty(stripeCustomerId))
        {
            try
            {
                customerSessionClientSecret = await stripe.CreateCustomerSessionAsync(stripeCustomerId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not create Stripe CustomerSession for customer {CustomerId}", stripeCustomerId);
            }
        }

        await using (var save = new NpgsqlCommand(
            "SELECT sp_upsert_stripe_intent(@b, @intent, @amount, @transfer, @cur)", connection))
        {
            save.Parameters.AddWithValue("b", bookingId);
            save.Parameters.AddWithValue("intent", intent.Id);
            save.Parameters.AddWithValue("amount", (int)total);
            save.Parameters.AddWithValue("transfer", (int)subtotal);
            save.Parameters.AddWithValue("cur", currency);
            await save.ExecuteScalarAsync(ct);
        }

        return new PaymentIntentResponse
        {
            ClientSecret = intent.ClientSecret,
            PublishableKey = stripe.PublishableKey,
            PaymentIntentId = intent.Id,
            Status = intent.Status,
            AmountCents = total,
            HoldExpiresAt = holdExpiresAt is { } h ? new DateTimeOffset(h, TimeSpan.Zero).ToUnixTimeSeconds() : 0,
            AchAllowed = achAllowed,
            CustomerSessionClientSecret = customerSessionClientSecret ?? string.Empty
        };
    }

    public override async Task<UpdatePaymentMethodResponse> UpdatePaymentIntentForMethod(
        UpdatePaymentMethodRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        if (!stripe.Configured)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Payments are not configured"));
        }
        if (!Guid.TryParse(request.BookingsId, out var bookingId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid booking id"));
        }
        var method = request.Method == "ach" ? "ach" : "card";

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        int total, fee, baseline;
        string? intentId;
        try
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT total_cents, fee_cents, baseline_total_cents "
                + "FROM sp_reprice_booking_for_method(@b, @u, @m)", connection);
            cmd.Parameters.AddWithValue("b", bookingId);
            cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
            cmd.Parameters.AddWithValue("m", method);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
            }
            total = reader.GetInt32(0);
            fee = reader.GetInt32(1);
            baseline = reader.GetInt32(2);
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }

        await using (var look = new NpgsqlCommand(
            "SELECT payment_intent_id FROM vw_stripe_transactions WHERE bookings_id = @b "
            + "AND status NOT IN ('Succeeded','Refunded')", connection))
        {
            look.Parameters.AddWithValue("b", bookingId);
            intentId = await look.ExecuteScalarAsync(ct) as string;
        }
        if (string.IsNullOrEmpty(intentId))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "No active payment to update"));
        }

        try
        {
            await stripe.UpdatePaymentIntentAmountAsync(intentId, total, fee, ct);
        }
        catch (StripeException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                $"Stripe update failed: {ex.StripeError?.Message ?? ex.Message}"));
        }

        await using (var save = new NpgsqlCommand(
            "SELECT sp_upsert_stripe_intent(@b, @intent, @amount, @transfer, @cur)", connection))
        {
            save.Parameters.AddWithValue("b", bookingId);
            save.Parameters.AddWithValue("intent", intentId);
            save.Parameters.AddWithValue("amount", total);
            save.Parameters.AddWithValue("transfer", total - fee);
            save.Parameters.AddWithValue("cur", "usd");
            await save.ExecuteScalarAsync(ct);
        }

        return new UpdatePaymentMethodResponse
        {
            TotalCents = total,
            SavingsCents = baseline - total
        };
    }

    public override async Task<PaymentStatusResponse> ConfirmFreeBooking(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        if (!Guid.TryParse(request.Value, out var bookingId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid booking id"));
        }

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        string status;
        long total;
        try
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT status, total_cents FROM sp_get_booking_for_payment(@b, @u)", connection);
            cmd.Parameters.AddWithValue("b", bookingId);
            cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
            }
            status = reader.GetString(0);
            total = reader.GetInt32(1);
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }

        if (status is "Paid" or "CheckedIn")
        {
            return new PaymentStatusResponse { BookingStatus = status, PaymentStatus = "Succeeded" };
        }
        if (total != 0)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                "This booking requires payment. Use card checkout."));
        }

        var qrToken = Convert.ToHexString(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        bool confirmed;
        await using (var confirm = new NpgsqlCommand("SELECT sp_confirm_booking(@b, @qr)", connection))
        {
            confirm.Parameters.AddWithValue("b", bookingId);
            confirm.Parameters.AddWithValue("qr", qrToken);
            confirmed = await confirm.ExecuteScalarAsync(ct) is true;
        }
        if (confirmed)
        {
            await BookingEmailSender.SendBookingConfirmationEmailAsync(
                connection, bookingId, email, templates, settings, logger, ct);
        }

        return new PaymentStatusResponse { BookingStatus = "Paid", PaymentStatus = "Succeeded" };
    }

    public override async Task<PaymentStatusResponse> GetPaymentStatus(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (!Guid.TryParse(request.Value, out var bookingId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid booking id"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT status, COALESCE(payment_status, '') FROM vw_bookings WHERE bookings_id = @b", connection);
        cmd.Parameters.AddWithValue("b", bookingId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
        }
        return new PaymentStatusResponse
        {
            BookingStatus = reader.GetString(0),
            PaymentStatus = reader.GetString(1)
        };
    }
}
