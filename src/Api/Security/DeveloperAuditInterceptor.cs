using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;

namespace TicketSpan.Api.Security;

public sealed class DeveloperAuditInterceptor : Interceptor
{
    private static readonly string[] ReadOnlyPrefixes = { "Get", "List", "Search", "Lookup", "Quote", "Check", "Validate" };

    private static readonly HashSet<string> NotifiableEventMethods = new(StringComparer.Ordinal)
    {
        "CreateEvent",
        "UpdateEvent",
        "ChangeEventStatus",
        "DeleteEvent",
    };

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var httpContext = context.GetHttpContext();
        var tenantContext = httpContext.RequestServices.GetRequiredService<TenantContext>();
        if (!tenantContext.IsActingForTenant || tenantContext.UsersId is null || tenantContext.TenantsId is null)
        {
            return await continuation(request, context);
        }
        var (serviceName, methodName) = SplitMethod(context.Method);
        if (IsReadOnly(methodName))
        {
            return await continuation(request, context);
        }
        var response = await continuation(request, context);
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<DeveloperAuditInterceptor>>();
        var db = httpContext.RequestServices.GetRequiredService<Db>();
        try
        {
            await WriteAuditLogAsync(db, tenantContext, httpContext, serviceName, methodName, request, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed writing developer audit log for {Method}", context.Method);
        }
        if (tenantContext.NotifyTenant && serviceName == "EventService" && NotifiableEventMethods.Contains(methodName))
        {
            try
            {
                await NotifyTenantAsync(httpContext, db, tenantContext, methodName, request, context.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed sending tenant notification for {Method}", context.Method);
            }
        }
        return response;
    }

    private static (string Service, string Method) SplitMethod(string fullMethod)
    {
        var segments = fullMethod.Split('/');
        if (segments.Length < 3)
        {
            return (fullMethod, fullMethod);
        }
        var service = segments[1][(segments[1].LastIndexOf('.') + 1)..];
        return (service, segments[2]);
    }

    private static bool IsReadOnly(string methodName) =>
        ReadOnlyPrefixes.Any(p => methodName.StartsWith(p, StringComparison.Ordinal));

    private static async Task WriteAuditLogAsync(
        Db db,
        TenantContext tenantContext,
        HttpContext httpContext,
        string serviceName,
        string methodName,
        object request,
        CancellationToken ct)
    {
        var metadataJson = request is IMessage message ? JsonFormatter.Default.Format(message) : "{}";
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_audit_log(@etype, @atype, @actor, @stype, @sid, @action, @meta, @ip, @corr, @event)", connection);
        cmd.Parameters.AddWithValue("etype", "developer_action");
        cmd.Parameters.AddWithValue("atype", "Developer");
        cmd.Parameters.AddWithValue("actor", tenantContext.UsersId!.Value);
        cmd.Parameters.AddWithValue("stype", serviceName);
        cmd.Parameters.AddWithValue("sid", (object?)ExtractEventId(request) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("action", methodName);
        cmd.Parameters.AddWithValue("meta", metadataJson);
        cmd.Parameters.AddWithValue("ip", (object?)httpContext.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("corr", tenantContext.CorrelationId);
        cmd.Parameters.AddWithValue("event", (object?)ExtractEventId(request) ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static Guid? ExtractEventId(object request)
    {
        var property = request.GetType().GetProperty("EventsId") ?? request.GetType().GetProperty("Value");
        return property?.GetValue(request) is string raw && Guid.TryParse(raw, out var id) ? id : null;
    }

    private static async Task NotifyTenantAsync(
        HttpContext httpContext,
        Db db,
        TenantContext tenantContext,
        string methodName,
        object request,
        CancellationToken ct)
    {
        var emailService = httpContext.RequestServices.GetRequiredService<IEmailService>();
        var settings = httpContext.RequestServices.GetRequiredService<AppSettingsProvider>();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        string tenantName;
        await using (var tenantCmd = new NpgsqlCommand("SELECT name FROM vw_tenants WHERE tenants_id = @t", connection))
        {
            tenantCmd.Parameters.AddWithValue("t", tenantContext.TenantsId!.Value);
            tenantName = await tenantCmd.ExecuteScalarAsync(ct) as string ?? "your workspace";
        }
        var adminEmails = new List<string>();
        await using (var memberCmd = new NpgsqlCommand(
            "SELECT email FROM sp_get_tenant_members(@t) WHERE role = @admin", connection))
        {
            memberCmd.Parameters.AddWithValue("t", tenantContext.TenantsId!.Value);
            memberCmd.Parameters.AddWithValue("admin", Lookups.UserRoles.Admin);
            await using var reader = await memberCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                adminEmails.Add(reader.GetString(0));
            }
        }
        if (adminEmails.Count == 0)
        {
            return;
        }
        var eventTitle = request.GetType().GetProperty("Title")?.GetValue(request) as string;
        var actionText = methodName switch
        {
            "CreateEvent" => "created a new event",
            "UpdateEvent" => "updated an event",
            "ChangeEventStatus" => "changed an event's status",
            "DeleteEvent" => "removed an event",
            _ => "updated your events",
        };
        var detail = string.IsNullOrEmpty(eventTitle) ? string.Empty : $" (<strong>{System.Net.WebUtility.HtmlEncode(eventTitle)}</strong>)";
        var subject = $"TicketSpan team update for {tenantName}";
        var htmlBody =
            $"<p>Hello,</p><p>The TicketSpan team {actionText}{detail} in your workspace <strong>{System.Net.WebUtility.HtmlEncode(tenantName)}</strong>.</p>"
            + "<p>You can review it in your admin dashboard under Events.</p>"
            + "<p>— TicketSpan</p>";
        var fromAddress = await settings.GetStringAsync("developer_notification_email", "noreply@ticketspan.com", ct);
        foreach (var email in adminEmails)
        {
            await emailService.SendAsync(fromAddress, email, subject, htmlBody, ct);
        }
    }
}
