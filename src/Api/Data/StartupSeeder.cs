using Npgsql;

namespace Svyne.Api.Data;

public sealed class StartupSeeder
{
    private readonly Db db;

    public StartupSeeder(Db db)
    {
        this.db = db;
    }

    private static readonly EnumGroup[] EnumGroups =
    {
        new("EventStatus", "events.status", "Lifecycle state of an event.",
            new[] { ("Draft", 0), ("Published", 1), ("Completed", 2), ("Cancelled", 3) }),
        new("EventCategory", "events.category", "Category an event belongs to.",
            new[] { ("Music", 0), ("Business", 1), ("Social", 2), ("Dining", 3), ("Tech", 4), ("Arts", 5), ("Family", 6), ("Sports", 7) }),
        new("LayoutMode", "events.layout_mode", "Seating layout strategy for an event.",
            new[] { ("Grid", 1), ("Open", 2) }),
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
    };

    private static readonly (string Key, string Value, string Description)[] AppSettings =
    {
        ("admin_invitation_email", "noreply@svyne.com", "From address for admin invitation emails."),
        ("admin_invitation_expiry", "86400", "Admin invitation validity window in seconds (24 hours)."),
        ("admin_invitation_subject", "You are invited to join svyne", "Subject line for admin invitation emails."),
        ("admin_invitation_link_base", "http://admin.localhost:5173/accept-invitation", "Frontend base URL for the admin invitation accept link."),
        ("tenant_setup_email", "noreply@svyne.com", "From address for tenant admin setup emails."),
        ("tenant_setup_subject", "Activate your Svyne workspace", "Subject line for tenant admin setup emails."),
        ("tenant_setup_link_base", "http://admin.localhost:5173/set-password", "Frontend base URL for the tenant admin setup link (admin portal /set-password). After setting password the admin is redirected to the admin login."),
        ("tenant_setup_expiry_days", "7", "Tenant admin setup link validity window in days."),
        ("password_reset_email", "noreply@svyne.com", "From address for password reset emails."),
        ("password_reset_subject", "Reset your Svyne password", "Subject line for password reset emails."),
        ("password_reset_link_base", "http://{slug}.localhost:5173/set-password", "Frontend base URL for the password reset link. {slug} is replaced by the tenant subdomain."),
        ("password_reset_expiry_hours", "1", "Password reset link validity window in hours."),
        ("booking_hold_seconds", "600", "Hard seat/table hold window in seconds while a booking awaits payment (10 minutes)."),
    };

    public async Task SeedAsync(CancellationToken ct)
    {
        await using var connection = await db.OpenBootstrapAsync(ct);

        foreach (var group in EnumGroups)
        {
            foreach (var (value, intValue) in group.Values)
            {
                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO enum_definitions (enum_definitions_id, enum_type, enum_value, int_value, used_in, description, created_at, updated_at) "
                    + "VALUES (gen_random_uuid(), @type, @value, @int, @used, @desc, now(), now()) "
                    + "ON CONFLICT (enum_type, enum_value) DO UPDATE SET int_value = EXCLUDED.int_value, used_in = EXCLUDED.used_in, description = EXCLUDED.description, updated_at = now()",
                    connection);
                cmd.Parameters.AddWithValue("type", group.EnumType);
                cmd.Parameters.AddWithValue("value", value);
                cmd.Parameters.AddWithValue("int", intValue);
                cmd.Parameters.AddWithValue("used", group.UsedIn);
                cmd.Parameters.AddWithValue("desc", group.Description);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        foreach (var (key, value, description) in AppSettings)
        {
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO app_settings (app_settings_id, key, value, description, created_at, updated_at) "
                + "VALUES (gen_random_uuid(), @key, @value, @desc, now(), now()) "
                + "ON CONFLICT (key) DO NOTHING",
                connection);
            cmd.Parameters.AddWithValue("key", key);
            cmd.Parameters.AddWithValue("value", value);
            cmd.Parameters.AddWithValue("desc", description);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Default service-fee formula: 6% of ticket price + $1.50 flat.
        await using (var formula = new NpgsqlCommand(
            "INSERT INTO fee_formulas (fee_formulas_id, name, percent_bps, flat_cents, is_active, created_at, updated_at) "
            + "SELECT gen_random_uuid(), @name, 600, 150, true, now(), now() "
            + "WHERE NOT EXISTS (SELECT 1 FROM fee_formulas WHERE name = @name)",
            connection))
        {
            formula.Parameters.AddWithValue("name", "Standard 6% + $1.50");
            await formula.ExecuteNonQueryAsync(ct);
        }
    }

    private sealed record EnumGroup(string EnumType, string UsedIn, string Description, (string Value, int Int)[] Values);
}
