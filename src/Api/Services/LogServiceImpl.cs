using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Admin;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class LogServiceImpl : LogService.LogServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public LogServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override Task<LogPage> GetAdminLogs(LogQuery request, ServerCallContext context)
        => QueryAsync("SELECT id, timestamp, action, entity_type, business_user_email, description FROM vw_business_logs ORDER BY timestamp DESC LIMIT @lim OFFSET @off", request, context);

    public override Task<LogPage> GetSystemLogs(LogQuery request, ServerCallContext context)
        => QueryAsync("SELECT id, timestamp, action, entity_type, user_email, category FROM vw_system_logs ORDER BY timestamp DESC LIMIT @lim OFFSET @off", request, context);

    public override Task<LogPage> GetDeveloperLogs(LogQuery request, ServerCallContext context)
        => QueryAsync("SELECT id, timestamp, request_method, request_path, severity, message FROM vw_developer_logs ORDER BY timestamp DESC LIMIT @lim OFFSET @off", request, context);

    private async Task<LogPage> QueryAsync(string sql, LogQuery request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (!tenantContext.IsDeveloper && tenantContext.TenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }
        var page = request.Page ?? new PageRequest();
        var response = new LogPage { Meta = new PageMeta { Offset = page.Offset, Limit = page.Limit } };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("lim", page.Limit <= 0 ? 50 : page.Limit);
        cmd.Parameters.AddWithValue("off", page.Offset);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Entries.Add(new LogEntry
            {
                Id = reader.GetGuid(0).ToString(),
                Timestamp = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero).ToUnixTimeSeconds(),
                Action = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                EntityType = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                ActorEmail = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Detail = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
            });
        }
        response.Meta.Total = response.Entries.Count;
        return response;
    }
}
