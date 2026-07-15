using Grpc.Core;
using Npgsql;

namespace TicketSpan.Api.Security;






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
