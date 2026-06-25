using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Admin;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class DashboardServiceImpl : DashboardService.DashboardServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public DashboardServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<AdminDashboard> GetAdminDashboard(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            // total_bookings = paid/checked-in seat count (actual attendees), not tenant users.
            "SELECT total_events, published_events, total_revenue_cents, total_bookings FROM vw_admin_dashboard_stats", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new AdminDashboard();
        }
        return new AdminDashboard
        {
            TotalEvents = reader.GetInt32(0),
            ActiveEvents = reader.GetInt32(1),
            TotalRevenueCents = reader.GetInt64(2),
            TotalAttendees = reader.GetInt32(3)
        };
    }

    public override async Task<DeveloperDashboard> GetDeveloperDashboard(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (!tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Developer access required"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        var result = new DeveloperDashboard();
        await using (var cmd = new NpgsqlCommand("SELECT total, active FROM sp_user_counts()", connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                result.TotalUsers = reader.GetInt32(0);
            }
        }
        await using (var cmd = new NpgsqlCommand("SELECT sp_count_tenants(NULL, false)", connection))
        {
            result.TotalTenants = (int)(await cmd.ExecuteScalarAsync(ct))!;
        }
        return result;
    }
}
