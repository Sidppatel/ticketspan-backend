using System.Text.Json;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;

namespace TicketSpan.Api.ErrorHandling;

public enum ErrorSeverity
{
    Critical,
    High,
    Medium,
    Low,
    Warning,
    Info
}

public sealed record ErrorContext
{
    public Guid? TenantsId { get; init; }
    public Guid? UsersId { get; init; }
    public string? RequestPath { get; init; }
    public string? RequestMethod { get; init; }
    public int? StatusCode { get; init; }
    public string? Ip { get; init; }
    public Guid? CorrelationId { get; init; }
    public string Source { get; init; } = "backend";
    public IReadOnlyDictionary<string, string>? Extra { get; init; }

    public static ErrorContext FromHttpContext(HttpContext httpContext, Security.TenantContext tenantContext)
        => new()
        {
            TenantsId = tenantContext.TenantsId,
            UsersId = tenantContext.UsersId,
            RequestPath = httpContext.Request.Path.ToString(),
            RequestMethod = httpContext.Request.Method,
            Ip = httpContext.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = tenantContext.CorrelationId
        };
}

public sealed class ErrorLogger
{
    private static readonly HttpClient SlackHttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly Db db;
    private readonly IEmailService emailService;
    private readonly ILogger<ErrorLogger> fallbackLogger;
    private readonly string environmentName;
    private readonly string? slackWebhookUrl;
    private readonly string? alertEmailTo;
    private readonly string alertEmailFrom;
    private readonly bool loggingEnabled;

    public ErrorLogger(Db db, IEmailService emailService, IConfiguration configuration,
        IHostEnvironment hostEnvironment, ILogger<ErrorLogger> fallbackLogger)
    {
        this.db = db;
        this.emailService = emailService;
        this.fallbackLogger = fallbackLogger;
        environmentName = hostEnvironment.EnvironmentName;
        slackWebhookUrl = configuration["ERROR_ALERTS_SLACK_WEBHOOK_URL"];
        alertEmailTo = configuration["ERROR_ALERTS_EMAIL_TO"];
        alertEmailFrom = configuration["ERROR_ALERTS_EMAIL_FROM"] ?? "noreply@localhost";
        loggingEnabled = configuration["ERROR_LOGGING_DISABLED"] != "true";
    }

    public Task<Guid?> LogErrorAsync(ErrorSeverity severity, string errorType, string message,
        Exception? exception = null, ErrorContext? context = null, CancellationToken ct = default)
        => WriteAsync(severity, errorType, message, exception, context, ct);

    public Task<Guid?> LogWarningAsync(string warningType, string message,
        ErrorContext? context = null, CancellationToken ct = default)
        => WriteAsync(ErrorSeverity.Warning, warningType, message, null, context, ct);

    public Task<Guid?> LogInfoAsync(string infoType, string message,
        ErrorContext? context = null, CancellationToken ct = default)
        => WriteAsync(ErrorSeverity.Info, infoType, message, null, context, ct);

    private async Task<Guid?> WriteAsync(ErrorSeverity severity, string errorType, string message,
        Exception? exception, ErrorContext? context, CancellationToken ct)
    {
        if (!loggingEnabled)
        {
            return null;
        }
        var fullMessage = exception is null ? message : $"{message}: {exception.Message}";
        try
        {
            var errorId = await InsertAsync(severity, errorType, fullMessage, exception, context, ct);
            await NotifyAsync(severity, errorType, fullMessage, context, errorId, ct);
            return errorId;
        }
        catch (Exception loggingFailure)
        {
            fallbackLogger.LogError(loggingFailure,
                "ErrorLogger failed to persist {Severity} {ErrorType}: {Message}", severity, errorType, fullMessage);
            if (exception is not null)
            {
                fallbackLogger.LogError(exception, "Original unpersisted error");
            }
            return null;
        }
    }

    private async Task<Guid> InsertAsync(ErrorSeverity severity, string errorType, string message,
        Exception? exception, ErrorContext? context, CancellationToken ct)
    {
        var extraJson = context?.Extra is { Count: > 0 } extra ? JsonSerializer.Serialize(extra) : null;
        await using var connection = await db.OpenBootstrapAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_log_system_error(@sev, @type, @msg, @stack, @path, @method, @status, @src, @tenant, @user, @ip, @corr, @extra)",
            connection);
        AddText(cmd, "sev", severity.ToString());
        AddText(cmd, "type", exception?.GetType().FullName ?? errorType);
        AddText(cmd, "msg", Truncate(message, 4000));
        AddText(cmd, "stack", Truncate(exception?.ToString(), 16000));
        AddText(cmd, "path", context?.RequestPath);
        AddText(cmd, "method", context?.RequestMethod);
        cmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlTypes.NpgsqlDbType.Integer)
        {
            Value = (object?)context?.StatusCode ?? DBNull.Value
        });
        AddText(cmd, "src", context?.Source ?? "backend");
        AddUuid(cmd, "tenant", context?.TenantsId);
        AddUuid(cmd, "user", context?.UsersId);
        AddText(cmd, "ip", context?.Ip);
        AddUuid(cmd, "corr", context?.CorrelationId);
        AddText(cmd, "extra", extraJson);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid id ? id : Guid.Empty;
    }

    private async Task NotifyAsync(ErrorSeverity severity, string errorType, string message,
        ErrorContext? context, Guid errorId, CancellationToken ct)
    {
        if (severity is not (ErrorSeverity.Critical or ErrorSeverity.High))
        {
            return;
        }
        var summary =
            $"{severity}: {errorType} [{environmentName}] {Truncate(message, 300)} " +
            $"| path={context?.RequestPath ?? "-"} tenant={context?.TenantsId?.ToString() ?? "-"} ref={errorId}";
        if (!string.IsNullOrEmpty(slackWebhookUrl))
        {
            try
            {
                using var response = await SlackHttpClient.PostAsJsonAsync(slackWebhookUrl, new { text = summary }, ct);
            }
            catch (Exception slackFailure)
            {
                fallbackLogger.LogWarning(slackFailure, "Slack error notification failed for {ErrorId}", errorId);
            }
        }
        if (severity == ErrorSeverity.Critical && !string.IsNullOrEmpty(alertEmailTo))
        {
            try
            {
                await emailService.SendAsync(alertEmailFrom, alertEmailTo,
                    $"[{environmentName}] Critical error: {errorType}",
                    $"<p>{System.Net.WebUtility.HtmlEncode(summary)}</p>", ct);
            }
            catch (Exception emailFailure)
            {
                fallbackLogger.LogWarning(emailFailure, "Email error notification failed for {ErrorId}", errorId);
            }
        }
    }

    private static void AddUuid(NpgsqlCommand cmd, string name, Guid? value)
        => cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlTypes.NpgsqlDbType.Uuid)
        {
            Value = (object?)value ?? DBNull.Value
        });

    private static void AddText(NpgsqlCommand cmd, string name, string? value)
        => cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = (object?)value ?? DBNull.Value
        });

    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];
}
