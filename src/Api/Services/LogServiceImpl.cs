using System.Runtime.CompilerServices;
using System.Text.Json;
using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using NpgsqlTypes;
using TicketSpan.Api.Data;
using TicketSpan.Api.ErrorHandling;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Admin;
using TicketSpan.Protos.Common;

namespace TicketSpan.Api.Services;

public sealed class LogServiceImpl : LogService.LogServiceBase
{
    private const int MaxClientReportsPerBatch = 20;
    private const int MaxClientReportsPerIpPerMinute = 60;
    private static readonly string[] AcceptedClientSeverities = ["High", "Medium", "Low", "Warning", "Info"];

    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly ErrorLogger errorLogger;
    private readonly IMemoryCache memoryCache;

    public LogServiceImpl(Db db, TenantContext tenantContext, ErrorLogger errorLogger, IMemoryCache memoryCache)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.errorLogger = errorLogger;
        this.memoryCache = memoryCache;
    }

    public override async Task<LogPage> GetAdminLogs(LogQuery request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (!tenantContext.IsDeveloper && tenantContext.TenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }
        var page = request.Page ?? new PageRequest();
        var take = page.Limit <= 0 || page.Limit > 200 ? 50 : page.Limit;
        var response = new LogPage { Meta = new PageMeta { Offset = page.Offset, Limit = take } };

        var filters = new List<string>();
        if (!string.IsNullOrEmpty(request.Action)) filters.Add("action = @action");
        if (!string.IsNullOrEmpty(request.EntityType)) filters.Add("entity_type = @entity_type");
        if (Guid.TryParse(request.EventsId, out _)) filters.Add("events_id = @events_id");
        if (request.From > 0) filters.Add("timestamp >= @from");
        if (request.To > 0) filters.Add("timestamp <= @to");
        if (!string.IsNullOrEmpty(page.Search))
        {
            filters.Add("(action ILIKE @q OR entity_type ILIKE @q OR coalesce(business_user_email, '') ILIKE @q OR coalesce(description, '') ILIKE @q)");
        }
        var where = filters.Count > 0 ? " WHERE " + string.Join(" AND ", filters) : string.Empty;

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        await using (var countCmd = new NpgsqlCommand($"SELECT COUNT(*) FROM vw_business_logs{where}", connection))
        {
            AddAdminLogFilters(countCmd, request, page);
            response.Meta.Total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct) ?? 0);
        }

        await using var cmd = new NpgsqlCommand(
            $"SELECT id, timestamp, action, entity_type, business_user_email, description, events_id FROM vw_business_logs{where} ORDER BY timestamp DESC LIMIT @lim OFFSET @off",
            connection);
        AddAdminLogFilters(cmd, request, page);
        cmd.Parameters.AddWithValue("lim", take);
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
                Detail = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                EventsId = reader.IsDBNull(6) ? string.Empty : reader.GetGuid(6).ToString()
            });
        }
        return response;
    }

    private static void AddAdminLogFilters(NpgsqlCommand cmd, LogQuery request, PageRequest page)
    {
        if (!string.IsNullOrEmpty(request.Action)) cmd.Parameters.AddWithValue("action", request.Action);
        if (!string.IsNullOrEmpty(request.EntityType)) cmd.Parameters.AddWithValue("entity_type", request.EntityType);
        if (Guid.TryParse(request.EventsId, out var eventsId)) cmd.Parameters.AddWithValue("events_id", eventsId);
        if (request.From > 0)
        {
            cmd.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz)
            {
                Value = DateTimeOffset.FromUnixTimeSeconds(request.From)
            });
        }
        if (request.To > 0)
        {
            cmd.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz)
            {
                Value = DateTimeOffset.FromUnixTimeSeconds(request.To)
            });
        }
        if (!string.IsNullOrEmpty(page.Search)) cmd.Parameters.AddWithValue("q", $"%{page.Search}%");
    }

    public override Task<LogPage> GetSystemLogs(LogQuery request, ServerCallContext context)
        => QueryAsync("SELECT id, timestamp, action, entity_type, user_email, category FROM vw_system_logs ORDER BY timestamp DESC LIMIT @lim OFFSET @off", request, context);

    public override Task<LogPage> GetDeveloperLogs(LogQuery request, ServerCallContext context)
        => QueryAsync("SELECT id, timestamp, request_method, request_path, severity, message FROM vw_developer_logs ORDER BY timestamp DESC LIMIT @lim OFFSET @off", request, context);

    public override async Task<ErrorLogPage> GetErrorLogs(ErrorLogQuery request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var page = request.Page ?? new PageRequest();
        var take = page.Limit <= 0 || page.Limit > 200 ? 50 : page.Limit;
        var response = new ErrorLogPage { Meta = new PageMeta { Offset = page.Offset, Limit = take } };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        await using (var countCmd = new NpgsqlCommand(
            "SELECT sp_count_error_logs(@sev, @src, @res, @q, @from, @to)", connection))
        {
            AddFilterParameters(countCmd, request);
            response.Meta.Total = (int)(await countCmd.ExecuteScalarAsync(ct) ?? 0);
        }

        await using var cmd = new NpgsqlCommand(
            "SELECT id, timestamp, severity, message, exception_type, stack_trace, request_path, request_method, "
            + "status_code, users_id, ip_address, correlation_id, metadata_json, tenants_id, source, resolved, "
            + "resolved_notes, resolved_by, resolved_at "
            + "FROM sp_get_error_logs(@sev, @src, @res, @q, @from, @to, @skip, @take)", connection);
        AddFilterParameters(cmd, request);
        cmd.Parameters.AddWithValue("skip", page.Offset);
        cmd.Parameters.AddWithValue("take", take);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Entries.Add(new ErrorLogEntry
            {
                Id = reader.GetGuid(0).ToString(),
                Timestamp = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero).ToUnixTimeSeconds(),
                Severity = GetStringOrEmpty(reader, 2),
                Message = GetStringOrEmpty(reader, 3),
                ExceptionType = GetStringOrEmpty(reader, 4),
                StackTrace = GetStringOrEmpty(reader, 5),
                RequestPath = GetStringOrEmpty(reader, 6),
                RequestMethod = GetStringOrEmpty(reader, 7),
                StatusCode = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                UsersId = reader.IsDBNull(9) ? string.Empty : reader.GetGuid(9).ToString(),
                IpAddress = GetStringOrEmpty(reader, 10),
                CorrelationId = GetStringOrEmpty(reader, 11),
                MetadataJson = GetStringOrEmpty(reader, 12),
                TenantsId = reader.IsDBNull(13) ? string.Empty : reader.GetGuid(13).ToString(),
                Source = GetStringOrEmpty(reader, 14),
                Resolved = !reader.IsDBNull(15) && reader.GetBoolean(15),
                ResolvedNotes = GetStringOrEmpty(reader, 16),
                ResolvedBy = GetStringOrEmpty(reader, 17),
                ResolvedAt = reader.IsDBNull(18)
                    ? 0
                    : new DateTimeOffset(reader.GetDateTime(18), TimeSpan.Zero).ToUnixTimeSeconds()
            });
        }
        return response;
    }

    public override async Task<ErrorLogStats> GetErrorLogStats(Empty request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_get_error_log_stats()", connection);
        var json = (string?)await cmd.ExecuteScalarAsync(ct) ?? "{}";
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var stats = new ErrorLogStats
        {
            TotalToday = GetInt(root, "total_today"),
            TotalWeek = GetInt(root, "total_week"),
            TotalMonth = GetInt(root, "total_month"),
            Unresolved = GetInt(root, "unresolved")
        };
        FillCounts(root, "by_severity", stats.BySeverity);
        FillCounts(root, "daily", stats.Daily);
        FillCounts(root, "top_types", stats.TopTypes);
        FillCounts(root, "top_tenants", stats.TopTenants);
        return stats;
    }

    public override async Task<AckResponse> ResolveErrorLog(ResolveErrorLogRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        if (!Guid.TryParse(request.ErrorLogId, out var errorLogId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid error log id"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_resolve_error_log(@id, @notes, @by)", connection);
        cmd.Parameters.AddWithValue("id", errorLogId);
        cmd.Parameters.Add(new NpgsqlParameter("notes", NpgsqlDbType.Text)
        {
            Value = (object?)Truncate(request.Notes, 2000) ?? DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("by", NpgsqlDbType.Uuid)
        {
            Value = (object?)tenantContext.UsersId ?? DBNull.Value
        });
        var resolved = (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
        return resolved
            ? new AckResponse { Success = true, Message = "Resolved" }
            : new AckResponse { Success = false, Message = "Error log not found", Code = 404 };
    }

    public override async Task<AckResponse> ReportClientErrors(ClientErrorBatch request, ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateLimitKey = $"client_error_rate:{ip}";
        var reportedThisMinute = memoryCache.GetOrCreate(rateLimitKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return new StrongBox<int>(0);
        })!;
        var accepted = 0;
        foreach (var report in request.Reports.Take(MaxClientReportsPerBatch))
        {
            if (Interlocked.Increment(ref reportedThisMinute.Value) > MaxClientReportsPerIpPerMinute)
            {
                break;
            }
            var severity = AcceptedClientSeverities.Contains(report.Severity) ? report.Severity : "Medium";
            await errorLogger.LogErrorAsync(
                Enum.Parse<ErrorSeverity>(severity),
                string.IsNullOrEmpty(report.ErrorType) ? "ClientError" : Truncate(report.ErrorType, 200)!,
                Truncate(report.Message, 2000) ?? "Client error",
                null,
                new ErrorContext
                {
                    TenantsId = tenantContext.TenantsId,
                    UsersId = tenantContext.UsersId,
                    RequestPath = Truncate(report.PageUrl, 500),
                    Ip = ip,
                    CorrelationId = tenantContext.CorrelationId,
                    Source = "frontend",
                    Extra = BuildClientExtra(report, httpContext)
                },
                context.CancellationToken);
            accepted++;
        }
        return new AckResponse { Success = true, Message = $"Accepted {accepted}" };
    }

    private static Dictionary<string, string> BuildClientExtra(ClientErrorReport report, HttpContext httpContext)
        => new()
        {
            ["page_url"] = Truncate(report.PageUrl, 500) ?? string.Empty,
            ["previous_url"] = Truncate(report.PreviousUrl, 500) ?? string.Empty,
            ["screen_size"] = Truncate(report.ScreenSize, 30) ?? string.Empty,
            ["viewport_size"] = Truncate(report.ViewportSize, 30) ?? string.Empty,
            ["session_id"] = Truncate(report.SessionId, 64) ?? string.Empty,
            ["stack_trace"] = Truncate(report.StackTrace, 8000) ?? string.Empty,
            ["breadcrumbs"] = Truncate(report.BreadcrumbsJson, 4000) ?? string.Empty,
            ["user_agent"] = Truncate(httpContext.Request.Headers.UserAgent.ToString(), 512) ?? string.Empty,
            ["occurred_at"] = report.OccurredAt.ToString()
        };

    private void RequireDeveloper()
    {
        if (!tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }
    }

    private static void AddFilterParameters(NpgsqlCommand cmd, ErrorLogQuery request)
    {
        AddNullableText(cmd, "sev", request.Severity);
        AddNullableText(cmd, "src", request.Source);
        cmd.Parameters.Add(new NpgsqlParameter("res", NpgsqlDbType.Boolean)
        {
            Value = request.ResolvedFilter switch
            {
                1 => false,
                2 => true,
                _ => DBNull.Value
            }
        });
        AddNullableText(cmd, "q", request.Search);
        cmd.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz)
        {
            Value = request.From > 0 ? DateTimeOffset.FromUnixTimeSeconds(request.From) : DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz)
        {
            Value = request.To > 0 ? DateTimeOffset.FromUnixTimeSeconds(request.To) : DBNull.Value
        });
    }

    private static void AddNullableText(NpgsqlCommand cmd, string name, string value)
        => cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text)
        {
            Value = string.IsNullOrEmpty(value) ? DBNull.Value : value
        });

    private static string GetStringOrEmpty(NpgsqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static int GetInt(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) && element.TryGetInt32(out var value) ? value : 0;

    private static void FillCounts(JsonElement root, string property,
        Google.Protobuf.Collections.RepeatedField<ErrorLogCount> target)
    {
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        foreach (var item in element.EnumerateObject())
        {
            target.Add(new ErrorLogCount { Key = item.Name, Count = item.Value.GetInt32() });
        }
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];

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
