using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace TicketSpan.Api.Data;

public sealed class StartupSeeder
{
    private readonly Db db;
    private readonly IConfiguration configuration;
    private readonly IHostEnvironment environment;

    public StartupSeeder(Db db, IConfiguration configuration, IHostEnvironment environment)
    {
        this.db = db;
        this.configuration = configuration;
        this.environment = environment;
    }

    private static readonly EnumGroup[] EnumGroups =
    {
        new("EventStatus", "events.status", "Lifecycle state of an event.",
            new[] { ("Draft", 0), ("Published", 1), ("Completed", 2), ("Cancelled", 3) }),

        new("LayoutMode", "events.layout_mode", "Seating layout strategy for an event.",
            new[] { ("Grid", 1), ("Open", 2) }),
        new("EventType", "events.event_type", "How an event sells: open ticket tiers, tables, or both.",
            new[] { ("Open", 0), ("Table", 1), ("Both", 2) }),
        new("PriceRuleScope", "price_rules.scope", "Whether a price rule targets one price or the whole event.",
            new[] { ("Price", 0), ("Event", 1) }),
        new("InvitationStatus", "invitations.status", "State of an admin/staff invitation.",
            new[] { ("Pending", 0), ("Accepted", 1), ("Revoked", 2), ("Expired", 3) }),
        new("BookingStatus", "bookings.status", "State of a ticket/table booking.",
            new[] { ("Pending", 0), ("Paid", 1), ("CheckedIn", 2), ("Cancelled", 3), ("Refunded", 4), ("Expired", 5) }),
        new("PaymentStatus", "stripe_transactions.status", "State of a Stripe payment.",
            new[] { ("RequiresConfirmation", 0), ("Succeeded", 1), ("Failed", 2), ("Refunded", 3) }),
        new("TicketStatus", "tickets.status", "State of an issued ticket.",
            new[] { ("Unassigned", 0), ("Invited", 1), ("Claimed", 2), ("CheckedIn", 3) }),
        new("TableStatus", "tables.status", "Availability state of a physical table.",
            new[] { ("Available", 0), ("Locked", 1), ("Booked", 2) }),
        new("TableShape", "event_tables.shape", "Shape of a table in the layout.",
            new[] { ("Round", 0), ("Rectangle", 1), ("Square", 2), ("Cocktail", 3) }),
        new("LogCategory", "logs.category", "Category of a system log entry.",
            new[] { ("EntityChange", 0), ("BackgroundWorker", 1), ("Cache", 2), ("MockService", 3), ("Migration", 4) }),
        new("LogSeverity", "logs.severity", "Severity of a system log entry.",
            new[] { ("Warning", 0), ("Error", 1), ("Critical", 2) }),
        new("AuditActorType", "audit_logs.actor_type", "Kind of actor that produced an audit event.",
            new[] { ("User", 0), ("Admin", 1), ("Developer", 2), ("System", 3) }),
        new("UserRole", "users.role", "The access level or role of a user.",
            new[] { ("Attendee", 0), ("Admin", 1), ("Staff", 2), ("SubTenant", 3), ("Developer", 99) }),
    };

    private (string Key, string Value, string Description)[] BuildAppSettings()
    {
        var isProd = environment.IsProduction() || string.Equals(configuration["APP_ENV"], "production", StringComparison.OrdinalIgnoreCase);
        var baseDomain = configuration["FRONTEND_BASE_DOMAIN"]
            ?? configuration["CORS_BASE_DOMAIN"]
            ?? (isProd ? "ticketspan.com" : "localhost:5173");

        var scheme = isProd ? "https" : "http";

        var adminHost = configuration["FRONTEND_ADMIN_URL"]?.TrimEnd('/')
            ?? (isProd ? $"{scheme}://admin.{baseDomain}" : $"{scheme}://admin.localhost:5173");

        var staffHost = configuration["FRONTEND_STAFF_URL"]?.TrimEnd('/')
            ?? (isProd ? $"{scheme}://staff.{baseDomain}" : $"{scheme}://staff.localhost:5173");

        var tenantLinkBase = isProd ? $"{scheme}://{{slug}}.{baseDomain}" : $"{scheme}://{{slug}}.localhost:5173";

        return new (string Key, string Value, string Description)[]
        {
            ("admin_invitation_email", "noreply@ticketspan.com", "From address for admin invitation emails."),
            ("admin_invitation_expiry", "86400", "Admin invitation validity window in seconds (24 hours)."),
            ("admin_invitation_subject", "You are invited to join ticketspan", "Subject line for admin invitation emails."),
            ("admin_invitation_link_base", $"{adminHost}/accept-invitation", "Frontend base URL for the admin invitation accept link."),
            ("tenant_setup_email", "noreply@ticketspan.com", "From address for tenant admin setup emails."),
            ("tenant_setup_subject", "Activate your TicketSpan workspace", "Subject line for tenant admin setup emails."),
            ("tenant_setup_link_base", $"{adminHost}/set-password", "Frontend base URL for the tenant admin setup link (admin portal /set-password). After setting password the admin is redirected to the admin login."),
            ("tenant_setup_expiry_days", "7", "Tenant admin setup link validity window in days."),
            ("password_reset_email", "noreply@ticketspan.com", "From address for password reset emails."),
            ("password_reset_subject", "Reset your TicketSpan password", "Subject line for password reset emails."),
            ("password_reset_link_base", $"{tenantLinkBase}/set-password", "Frontend base URL for the password reset link. {slug} is replaced by the tenant subdomain."),
            ("password_reset_expiry_hours", "1", "Password reset link validity window in hours."),
            ("booking_hold_seconds", "600", "Hard seat/table hold window in seconds while a booking awaits payment (10 minutes)."),
            ("default_timezone", "America/Chicago", "Default timezone for date and time calculations across the platform."),
            ("event_image_aspect_ratio", "16:9", "Crop and display aspect ratio for event page images."),
            ("event_thumbnail_aspect_ratio", "4:3", "Crop and display aspect ratio for event list thumbnails."),
            ("sponsor_image_aspect_ratio", "1:1", "Crop and display aspect ratio for sponsor logos."),
            ("performer_image_aspect_ratio", "1:1", "Crop and display aspect ratio for performer photos."),
            ("venue_image_aspect_ratio", "16:9", "Crop and display aspect ratio for venue photos."),
            ("floorplan_default_size", "80", "Default width and height for floor plan objects in pixels."),
            ("floorplan_canvas_width", "1200", "Default floor plan designer canvas width in pixels."),
            ("floorplan_canvas_height", "800", "Default floor plan designer canvas height in pixels."),
            ("floorplan_default_color", "#059669", "Default accent color for floor plan objects."),
            ("magic_link_email", "noreply@ticketspan.com", "From address for attendee magic link sign-in emails."),
            ("magic_link_subject", "Your TicketSpan Sign-In Link", "Subject line for attendee magic link emails."),
            ("magic_link_base", $"{tenantLinkBase}/magic-login", "Base URL template for magic sign-in links."),
            ("ticket_claim_link_base", $"{tenantLinkBase}/tickets/claim", "Base URL template for ticket claim transfers."),
            ("staff_portal_link_base", staffHost, "Base URL for staff portal invitations."),
            ("event_link_base", $"{tenantLinkBase}/e/{{eventId}}", "Base URL template for attendee event links."),
            ("developer_notification_email", "noreply@ticketspan.com", "From address for developer security audit alerts."),
            ("event_reminder", "[168, 48]", "reminder hours"),
        };
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        await using var connection = await db.OpenBootstrapAsync(ct);

        foreach (var group in EnumGroups)
        {
            foreach (var (value, intValue) in group.Values)
            {
                await using var cmd = new NpgsqlCommand(
                    "SELECT sp_seed_enum_definition(@type, @value, @int, @used, @desc)", connection);
                cmd.Parameters.AddWithValue("type", group.EnumType);
                cmd.Parameters.AddWithValue("value", value);
                cmd.Parameters.AddWithValue("int", intValue);
                cmd.Parameters.AddWithValue("used", group.UsedIn);
                cmd.Parameters.AddWithValue("desc", group.Description);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        var appSettings = BuildAppSettings();
        foreach (var (key, value, description) in appSettings)
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT sp_seed_app_setting(@key, @value, @desc)", connection);
            cmd.Parameters.AddWithValue("key", key);
            cmd.Parameters.AddWithValue("value", value);
            cmd.Parameters.AddWithValue("desc", description);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var defaults = new NpgsqlCommand("SELECT sp_seed_platform_defaults()", connection))
        {
            await defaults.ExecuteNonQueryAsync(ct);
        }
    }

    private sealed record EnumGroup(string EnumType, string UsedIn, string Description, (string Value, int Int)[] Values);
}
