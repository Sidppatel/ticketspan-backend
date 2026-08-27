using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;
using TicketSpan.Api.ErrorHandling;

namespace TicketSpan.Api.Payments;

public sealed class BillingWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly Db db;
    private readonly IEmailService emailService;
    private readonly EmailTemplateRenderer templates;
    private readonly ILogger<BillingWorker> logger;
    private readonly ErrorLogger errorLogger;

    public BillingWorker(Db db, IEmailService emailService, EmailTemplateRenderer templates, ILogger<BillingWorker> logger, ErrorLogger errorLogger)
    {
        this.db = db;
        this.emailService = emailService;
        this.templates = templates;
        this.logger = logger;
        this.errorLogger = errorLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await errorLogger.LogErrorAsync(
                        ErrorSeverity.Medium,
                        "BillingSweepFailure",
                        "Billing sweep failed",
                        ex,
                        new ErrorContext { RequestPath = "background:BillingWorker" },
                        CancellationToken.None);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {

        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var connection = await db.OpenBootstrapAsync(ct);

        await using (var cmd = new NpgsqlCommand("SELECT sp_expire_trials()", connection))
        {
            var expired = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            if (expired > 0) logger.LogInformation("Expired {Count} trial(s)", expired);
        }
        await using (var cmd = new NpgsqlCommand("SELECT sp_renew_subscriptions()", connection))
        {
            var renewed = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            if (renewed > 0) logger.LogInformation("Processed {Count} subscription renewal(s)", renewed);
        }
        await using (var cmd = new NpgsqlCommand("SELECT sp_renew_addons()", connection))
        {
            var renewed = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            if (renewed > 0) logger.LogInformation("Processed {Count} add-on renewal(s)", renewed);
        }

        await SendTrialRemindersAsync(connection, ct);
    }

    private async Task SendTrialRemindersAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var reminders = new List<(Guid SubscriptionId, Guid TenantId, int Day, DateTime EndsAt)>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT tenant_subscriptions_id, tenants_id, reminder_day, trial_ends_at FROM sp_trial_reminders_due()", connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                reminders.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetDateTime(3)));
            }
        }

        foreach (var reminder in reminders)
        {
            string? email = null;
            string? tenantName = null;
            await using (var cmd = new NpgsqlCommand(
                "SELECT email, tenant_name FROM sp_tenant_admin_contact(@t, @adminRole)", connection))
            {
                cmd.Parameters.AddWithValue("t", reminder.TenantId);
                cmd.Parameters.AddWithValue("adminRole", Lookups.UserRoles.Admin);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    email = reader.GetString(0);
                    tenantName = reader.GetString(1);
                }
            }

            if (email is not null)
            {
                var daysLeft = Math.Max((int)Math.Ceiling((reminder.EndsAt - DateTime.UtcNow).TotalDays), 0);
                var subject = $"Your TicketSpan trial ends in {daysLeft} day{(daysLeft == 1 ? "" : "s")}";
                var values = new Dictionary<string, string>
                {
                    ["Subject"] = subject,
                    ["TenantName"] = tenantName ?? "your workspace",
                    ["DaysLeft"] = daysLeft.ToString(),
                    ["TrialEndDate"] = reminder.EndsAt.ToString("MMMM d, yyyy"),
                    ["BillingLink"] = "http://admin.localhost:5173/billing"
                };
                var htmlBody = await templates.RenderAsync("trial_reminder.html", values, ct);
                await emailService.SendAsync("noreply@ticketspan.com", email, subject, htmlBody, ct);
            }

            await using var markCmd = new NpgsqlCommand("SELECT sp_mark_trial_reminder(@id, @day)", connection);
            markCmd.Parameters.AddWithValue("id", reminder.SubscriptionId);
            markCmd.Parameters.AddWithValue("day", reminder.Day);
            await markCmd.ExecuteNonQueryAsync(ct);
        }
    }
}
