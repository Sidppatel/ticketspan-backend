using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;
using TicketSpan.Api.ErrorHandling;

namespace TicketSpan.Api.Services;

public sealed class EventReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly Db db;
    private readonly IEmailService emailService;
    private readonly EmailTemplateRenderer templates;
    private readonly AppSettingsProvider settings;
    private readonly IConfiguration configuration;
    private readonly ILogger<EventReminderWorker> logger;
    private readonly ErrorLogger errorLogger;

    public EventReminderWorker(
        Db db,
        IEmailService emailService,
        EmailTemplateRenderer templates,
        AppSettingsProvider settings,
        IConfiguration configuration,
        ILogger<EventReminderWorker> logger,
        ErrorLogger errorLogger)
    {
        this.db = db;
        this.emailService = emailService;
        this.templates = templates;
        this.settings = settings;
        this.configuration = configuration;
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
                    await SweepRemindersAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await errorLogger.LogErrorAsync(
                        ErrorSeverity.Medium,
                        "EventReminderSweepFailure",
                        "Event reminder sweep failed",
                        ex,
                        new ErrorContext { RequestPath = "background:EventReminderWorker" },
                        CancellationToken.None);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Worker is stopping
        }
    }

    private async Task SweepRemindersAsync(CancellationToken ct)
    {
        await using var connection = await db.OpenBootstrapAsync(ct);

        var eventsDue = new List<(Guid EventId, Guid TenantId, string Title, DateTime StartDate, string VenueName, string VenueAddress, string TenantSlug, string ReminderType, int TargetHours)>();

        await using (var cmd = new NpgsqlCommand("SELECT events_id, tenants_id, title, start_date, venue_name, venue_address, tenant_slug, reminder_type, target_hours FROM sp_get_events_due_for_reminder()", connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                eventsDue.Add((
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetDateTime(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetInt32(8)
                ));
            }
        }

        if (eventsDue.Count == 0)
        {
            return;
        }

        logger.LogInformation("Processing automated reminders for {Count} event(s)", eventsDue.Count);

        var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@ticketspan.com", ct);
        var eventLinkTemplate = await settings.GetStringAsync("event_link_base", "http://{slug}.localhost:5173/e/{eventId}", ct);

        foreach (var ev in eventsDue)
        {
            var attendees = new List<(string Email, string EventTitle, DateTime StartDate, string VenueName, string VenueAddress, string TenantSlug, string TenantName)>();

            await using (var attCmd = new NpgsqlCommand(
                "SELECT email, event_title, start_date, venue_name, venue_address, tenant_slug, tenant_name FROM sp_get_event_attendee_emails(@id)", connection))
            {
                attCmd.Parameters.AddWithValue("id", ev.EventId);
                await using var attReader = await attCmd.ExecuteReaderAsync(ct);
                while (await attReader.ReadAsync(ct))
                {
                    attendees.Add((
                        attReader.GetString(0),
                        attReader.GetString(1),
                        attReader.GetDateTime(2),
                        attReader.GetString(3),
                        attReader.GetString(4),
                        attReader.GetString(5),
                        attReader.GetString(6)
                    ));
                }
            }

            var timeText = ev.TargetHours % 24 == 0 && ev.TargetHours >= 24
                ? $"{ev.TargetHours / 24} day{(ev.TargetHours / 24 == 1 ? "" : "s")}"
                : $"{ev.TargetHours} hour{(ev.TargetHours == 1 ? "" : "s")}";
            var reminderLabel = $"{timeText} to Go";
            var subject = $"Reminder: {ev.Title} is in {timeText}!";

            foreach (var att in attendees)
            {
                try
                {
                    var slug = string.IsNullOrEmpty(att.TenantSlug) ? "app" : att.TenantSlug;
                    var eventLink = eventLinkTemplate.Replace("{slug}", slug).Replace("{eventId}", ev.EventId.ToString());

                    var values = new Dictionary<string, string>
                    {
                        ["Subject"] = subject,
                        ["ReminderBadge"] = reminderLabel,
                        ["EventTitle"] = att.EventTitle,
                        ["EventDate"] = att.StartDate.ToString("f"),
                        ["VenueName"] = att.VenueName,
                        ["VenueAddress"] = string.IsNullOrEmpty(att.VenueAddress) ? "See event details for venue directions" : att.VenueAddress,
                        ["EventLink"] = eventLink,
                        ["TenantName"] = att.TenantName
                    };

                    var htmlBody = await templates.RenderAsync("event_reminder.html", values, ct);
                    await emailService.SendAsync(fromAddress, att.Email, subject, htmlBody, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send {ReminderType} reminder email to {Email} for Event: {EventId}", ev.ReminderType, att.Email, ev.EventId);
                }
            }

            await using (var markCmd = new NpgsqlCommand("SELECT sp_mark_event_reminder_sent(@id, @type)", connection))
            {
                markCmd.Parameters.AddWithValue("id", ev.EventId);
                markCmd.Parameters.AddWithValue("type", ev.ReminderType);
                await markCmd.ExecuteNonQueryAsync(ct);
            }

            logger.LogInformation("Completed {ReminderType} automated reminders for Event: {EventId} ({Count} emails sent)", ev.ReminderType, ev.EventId, attendees.Count);
        }
    }
}
