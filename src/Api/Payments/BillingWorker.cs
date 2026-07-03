using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Email;
using Svyne.Api.ErrorHandling;

namespace Svyne.Api.Payments;

/// <summary>
/// Hourly billing sweep: expires overdue 14-day trials back to the free tier,
/// renews subscriptions/add-ons past their period end (charges land in the
/// billing_charges ledger), and emails day-10 / day-13 trial reminders to the
/// tenant's admin. Runs on the bootstrap connection: state transitions span
/// tenants and RLS on billing tables is developer-only.
/// </summary>
public sealed class BillingWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly Db db;
    private readonly IEmailService emailService;
    private readonly ILogger<BillingWorker> logger;
    private readonly ErrorLogger errorLogger;

    public BillingWorker(Db db, IEmailService emailService, ILogger<BillingWorker> logger, ErrorLogger errorLogger)
    {
        this.db = db;
        this.emailService = emailService;
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
            // Host shutting down — expected.
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
                "SELECT u.email, t.name FROM users u JOIN tenants t ON t.tenants_id = u.tenants_id "
                + "WHERE u.tenants_id = @t AND u.role = 1 ORDER BY u.created_at LIMIT 1", connection))
            {
                cmd.Parameters.AddWithValue("t", reminder.TenantId);
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
                await emailService.SendAsync(
                    "noreply@svyne.com", email,
                    $"Your svyne trial ends in {daysLeft} day{(daysLeft == 1 ? "" : "s")}",
                    $"<p>Hi {tenantName},</p><p>Your 14-day Professional trial ends on "
                    + $"{reminder.EndsAt:MMMM d, yyyy}. Subscribe to keep Advanced Analytics and your "
                    + "trial features — or do nothing and your account returns to the free plan.</p>",
                    ct);
            }

            await using var markCmd = new NpgsqlCommand("SELECT sp_mark_trial_reminder(@id, @day)", connection);
            markCmd.Parameters.AddWithValue("id", reminder.SubscriptionId);
            markCmd.Parameters.AddWithValue("day", reminder.Day);
            await markCmd.ExecuteNonQueryAsync(ct);
        }
    }
}
