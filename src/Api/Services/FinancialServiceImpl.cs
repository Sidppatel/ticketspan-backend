using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Payments;
using Svyne.Api.Security;
using Svyne.Protos.Admin;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class FinancialServiceImpl : FinancialService.FinancialServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly IConfiguration configuration;
    private readonly StripeService stripe;

    public FinancialServiceImpl(Db db, TenantContext tenantContext, IConfiguration configuration, StripeService stripe)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.configuration = configuration;
        this.stripe = stripe;
    }

    public override async Task<MonthlyReport> GetMonthlyReport(MonthlyReportRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT COALESCE(SUM(charged_cents),0), COALESCE(SUM(platform_fee_cents),0), COALESCE(SUM(admin_payout_cents),0), COALESCE(SUM(booking_count),0) "
            + "FROM sp_get_monthly_report_by_event(@year, @month) WHERE @ev = '00000000-0000-0000-0000-000000000000' OR events_id = @ev", connection);
        cmd.Parameters.AddWithValue("year", request.Year);
        cmd.Parameters.AddWithValue("month", request.Month);
        cmd.Parameters.AddWithValue("ev", string.IsNullOrEmpty(request.EventsId) ? Guid.Empty : Guid.Parse(request.EventsId));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new MonthlyReport();
        }
        var gross = reader.GetInt64(0);
        var fees = reader.GetInt64(1);
        return new MonthlyReport
        {
            GrossCents = gross,
            FeesCents = fees,
            NetCents = reader.GetInt64(2),
            TicketsSold = (int)reader.GetInt64(3)
        };
    }

    public override async Task<StripeStatus> GetStripeStatus(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        // Pull the connected account id so we can refresh capability flags live
        // from Stripe. Locally there are no account.updated webhooks, so the DB
        // flags would otherwise stay stale after onboarding completes.
        string? accountId = null;
        await using (var look = new NpgsqlCommand(
            "SELECT stripe_connected_account_id FROM tenants WHERE tenants_id = @t", connection))
        {
            look.Parameters.AddWithValue("t", Guid.Parse(request.Value));
            accountId = await look.ExecuteScalarAsync(ct) as string;
        }

        if (!string.IsNullOrEmpty(accountId) && stripe.Configured)
        {
            try
            {
                var account = await stripe.GetAccountAsync(accountId, ct);
                await using var sync = new NpgsqlCommand(
                    "SELECT sp_update_tenant_stripe_status(@acct, @charges, @payouts, @details, @req)", connection);
                sync.Parameters.AddWithValue("acct", account.Id);
                sync.Parameters.AddWithValue("charges", account.ChargesEnabled);
                sync.Parameters.AddWithValue("payouts", account.PayoutsEnabled);
                sync.Parameters.AddWithValue("details", account.DetailsSubmitted);
                var reqJson = account.Requirements?.CurrentlyDue is { } due
                    ? System.Text.Json.JsonSerializer.Serialize(due)
                    : null;
                sync.Parameters.Add(new NpgsqlParameter("req", NpgsqlTypes.NpgsqlDbType.Jsonb)
                {
                    Value = (object?)reqJson ?? DBNull.Value
                });
                await sync.ExecuteNonQueryAsync(ct);
                return new StripeStatus
                {
                    ChargesEnabled = account.ChargesEnabled,
                    PayoutsEnabled = account.PayoutsEnabled,
                    DetailsSubmitted = account.DetailsSubmitted
                };
            }
            catch (Stripe.StripeException)
            {
                // Fall back to the persisted flags below if Stripe is unreachable.
            }
        }

        await using var cmd = new NpgsqlCommand(
            "SELECT stripe_charges_enabled, stripe_payouts_enabled, stripe_details_submitted FROM sp_get_tenant_stripe_status(@t)", connection);
        cmd.Parameters.AddWithValue("t", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new StripeStatus();
        }
        return new StripeStatus
        {
            ChargesEnabled = reader.GetBoolean(0),
            PayoutsEnabled = reader.GetBoolean(1),
            DetailsSubmitted = reader.GetBoolean(2)
        };
    }

    public override async Task<StripeOnboardingLink> StartStripeOnboarding(UuidValue request, ServerCallContext context)
    {
        if (!tenantContext.IsDeveloper && tenantContext.TenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }
        if (!stripe.Configured)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Payments are not configured"));
        }
        var ct = context.CancellationToken;
        var tenantId = Guid.Parse(request.Value);

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        // Reuse the tenant's connected account or create a fresh Express one,
        // pre-filled from the prefill captured on the tenant record.
        string? accountId;
        var prefill = new StripeAccountPrefill();
        await using (var cmd = new NpgsqlCommand(
            "SELECT t.stripe_connected_account_id, t.country_code, COALESCE(t.legal_name, t.name), "
            + "p.business_type, p.business_url, p.product_description, p.mcc, p.support_email "
            + "FROM tenants t LEFT JOIN tenant_stripe_profiles p ON p.tenants_id = t.tenants_id "
            + "WHERE t.tenants_id = @t", connection))
        {
            cmd.Parameters.AddWithValue("t", tenantId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Tenant not found"));
            }
            accountId = reader.IsDBNull(0) ? null : reader.GetString(0);
            prefill = new StripeAccountPrefill
            {
                Country = reader.IsDBNull(1) ? "US" : reader.GetString(1),
                BusinessName = reader.IsDBNull(2) ? null : reader.GetString(2),
                BusinessType = reader.IsDBNull(3) ? null : reader.GetString(3),
                Url = reader.IsDBNull(4) ? null : reader.GetString(4),
                ProductDescription = reader.IsDBNull(5) ? null : reader.GetString(5),
                Mcc = reader.IsDBNull(6) ? null : reader.GetString(6),
                SupportEmail = reader.IsDBNull(7) ? null : reader.GetString(7)
            };
        }

        if (string.IsNullOrEmpty(accountId))
        {
            accountId = await stripe.CreateExpressAccountAsync(prefill, ct);
            await using var save = new NpgsqlCommand(
                "SELECT sp_update_tenant_stripe_account(@t, @acct)", connection);
            save.Parameters.AddWithValue("t", tenantId);
            save.Parameters.AddWithValue("acct", accountId);
            await save.ExecuteNonQueryAsync(ct);
        }

        var baseUrl = configuration["PUBLIC_BASE_URL"] ?? "https://localhost";
        var returnUrl = $"{baseUrl}/stripe/onboard/return?tenant={tenantId}";
        var refreshUrl = $"{baseUrl}/stripe/onboard/refresh?tenant={tenantId}";
        var url = await stripe.CreateAccountLinkAsync(accountId, returnUrl, refreshUrl, ct);
        return new StripeOnboardingLink { Url = url };
    }
}
