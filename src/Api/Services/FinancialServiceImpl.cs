using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Admin;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class FinancialServiceImpl : FinancialService.FinancialServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly IConfiguration configuration;

    public FinancialServiceImpl(Db db, TenantContext tenantContext, IConfiguration configuration)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.configuration = configuration;
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

    public override Task<StripeOnboardingLink> StartStripeOnboarding(UuidValue request, ServerCallContext context)
    {
        if (!tenantContext.IsDeveloper && tenantContext.TenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }
        var baseUrl = configuration["PUBLIC_BASE_URL"] ?? "https://localhost";
        return Task.FromResult(new StripeOnboardingLink { Url = $"{baseUrl}/stripe/onboard/{request.Value}" });
    }
}
