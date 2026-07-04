using Grpc.Core;
using Npgsql;

namespace Svyne.Api.Security;

// Guards a single event-scoped RPC against event-scoped roles (check-in staff /
// event managers) reaching an event they were not assigned. RLS already covers
// direct-table DML, but reads that flow through ep_dev-owned views bypass RLS and
// are filtered only by their event-id parameter, so those read paths need this
// explicit check. No-op for full admins / developers.
public static class EventAccess
{
    public static async Task RequireAsync(
        NpgsqlConnection connection, TenantContext tenantContext, Guid eventId, CancellationToken ct)
    {
        if (!tenantContext.IsEventScoped)
        {
            return;
        }
        await using var cmd = new NpgsqlCommand("SELECT app.can_access_event(@ev)", connection);
        cmd.Parameters.AddWithValue("ev", eventId);
        if (await cmd.ExecuteScalarAsync(ct) is not true)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "No access to this event"));
        }
    }

    // For RPCs keyed by something other than an event id (a ticket, a booking):
    // eventIdScalarSql must be a scalar subquery returning the events_id for the
    // @key parameter. A missing row resolves to NULL, which can_access_event denies.
    public static async Task RequireResolvedAsync(
        NpgsqlConnection connection, TenantContext tenantContext, string eventIdScalarSql, Guid key, CancellationToken ct)
    {
        if (!tenantContext.IsEventScoped)
        {
            return;
        }
        await using var cmd = new NpgsqlCommand($"SELECT app.can_access_event(({eventIdScalarSql}))", connection);
        cmd.Parameters.AddWithValue("key", key);
        if (await cmd.ExecuteScalarAsync(ct) is not true)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "No access to this event"));
        }
    }
}
