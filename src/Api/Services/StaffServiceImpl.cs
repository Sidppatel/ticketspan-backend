using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Api.Email;
using TicketSpan.Protos.Admin;
using TicketSpan.Protos.Common;

namespace TicketSpan.Api.Services;

public sealed class StaffServiceImpl : StaffService.StaffServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly IEmailService emailService;
    private readonly EmailTemplateRenderer templates;
    private readonly AppSettingsProvider settings;

    public StaffServiceImpl(
        Db db,
        TenantContext tenantContext,
        IEmailService emailService,
        EmailTemplateRenderer templates,
        AppSettingsProvider settings)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.emailService = emailService;
        this.templates = templates;
        this.settings = settings;
    }

    public override async Task<ListStaffResponse> ListStaffForEvent(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListStaffResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT users_id, first_name, last_name, email, user_role, access_start, access_end FROM sp_list_staff_for_event(@ev)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var astart = reader.IsDBNull(5) ? 0 : new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero).ToUnixTimeSeconds();
            var aend = reader.IsDBNull(6) ? 0 : new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero).ToUnixTimeSeconds();
            response.Staff.Add(new StaffMember
            {
                UsersId = reader.GetGuid(0).ToString(),
                FirstName = reader.GetString(1),
                LastName = reader.GetString(2),
                Email = reader.GetString(3),
                Role = reader.GetInt32(4),
                AccessStart = astart,
                AccessEnd = aend
            });
        }
        return response;
    }

    public override async Task<AckResponse> AssignStaff(AssignStaffRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_assign_user_event(@u, @ev, @by, @astart::timestamptz, @aend::timestamptz)", connection);
        cmd.Parameters.AddWithValue("u", Guid.Parse(request.UsersId));
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("by", (object?)tenantContext.UsersId ?? DBNull.Value);
        cmd.Parameters.Add(new NpgsqlParameter("astart", NpgsqlTypes.NpgsqlDbType.TimestampTz)
        {
            Value = request.AccessStart > 0 ? DateTimeOffset.FromUnixTimeSeconds(request.AccessStart).UtcDateTime : DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("aend", NpgsqlTypes.NpgsqlDbType.TimestampTz)
        {
            Value = request.AccessEnd > 0 ? DateTimeOffset.FromUnixTimeSeconds(request.AccessEnd).UtcDateTime : DBNull.Value
        });
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Staff assigned" };
    }

    public override async Task<AckResponse> UpdateStaffAccessWindow(UpdateStaffAccessWindowRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_assign_user_event(@u, @ev, @by, @astart::timestamptz, @aend::timestamptz)", connection);
        cmd.Parameters.AddWithValue("u", Guid.Parse(request.UsersId));
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("by", (object?)tenantContext.UsersId ?? DBNull.Value);
        cmd.Parameters.Add(new NpgsqlParameter("astart", NpgsqlTypes.NpgsqlDbType.TimestampTz)
        {
            Value = request.AccessStart > 0 ? DateTimeOffset.FromUnixTimeSeconds(request.AccessStart).UtcDateTime : DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("aend", NpgsqlTypes.NpgsqlDbType.TimestampTz)
        {
            Value = request.AccessEnd > 0 ? DateTimeOffset.FromUnixTimeSeconds(request.AccessEnd).UtcDateTime : DBNull.Value
        });
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Access window updated" };
    }

    public override async Task<AckResponse> UnassignStaff(AssignStaffRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_unassign_user_event(@u, @ev)", connection);
        cmd.Parameters.AddWithValue("u", Guid.Parse(request.UsersId));
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Staff unassigned" };
    }

    public override async Task<ListStaffResponse> ListAllStaff(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var response = new ListStaffResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT users_id, email, display_name, role FROM sp_get_tenant_members(@t)", connection);
        cmd.Parameters.AddWithValue("t", tenantContext.TenantsId!);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var displayName = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var parts = displayName.Split(' ', 2);
            var first = parts.Length > 0 ? parts[0] : "";
            var last = parts.Length > 1 ? parts[1] : "";
            response.Staff.Add(new StaffMember
            {
                UsersId = reader.GetGuid(0).ToString(),
                FirstName = first,
                LastName = last,
                Email = reader.GetString(1),
                Role = reader.GetInt16(3)
            });
        }
        return response;
    }

    public override async Task<AssignStaffByEmailResponse> AssignStaffByEmail(AssignStaffByEmailRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var targetRole = request.Role == Lookups.UserRoles.EventManager ? Lookups.UserRoles.EventManager : Lookups.UserRoles.Staff;
        var roleName = targetRole == Lookups.UserRoles.EventManager ? "Event Manager" : "Check-in Staff";
        var emailHash = EmailHasher.Hash(request.Email);
        var eventId = Guid.Parse(request.EventsId);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        string eventTitle = "Your Event";
        DateTime startDate = DateTime.UtcNow;
        string venueName = "";
        await using (var evCmd = new NpgsqlCommand(
            "SELECT e.title, e.start_date, v.name FROM events e LEFT JOIN venues v ON v.venues_id = e.venues_id WHERE e.events_id = @ev", connection))
        {
            evCmd.Parameters.AddWithValue("ev", eventId);
            await using var evReader = await evCmd.ExecuteReaderAsync(ct);
            if (await evReader.ReadAsync(ct))
            {
                eventTitle = evReader.IsDBNull(0) ? "Your Event" : evReader.GetString(0);
                startDate = evReader.IsDBNull(1) ? DateTime.UtcNow : evReader.GetDateTime(1);
                venueName = evReader.IsDBNull(2) ? "" : evReader.GetString(2);
            }
        }

        await using var lookup = new NpgsqlCommand(
            "SELECT users_id FROM sp_get_user_by_email_hash(@h) LIMIT 1", connection);
        lookup.Parameters.AddWithValue("h", emailHash);
        var userExistsId = await lookup.ExecuteScalarAsync(ct);

        if (userExistsId is Guid userId)
        {
            await using var promoteCmd = new NpgsqlCommand(
                "SELECT sp_set_user_role(@id, @role, ARRAY[@attendeeRole, @staffRole])", connection);
            promoteCmd.Parameters.AddWithValue("id", userId);
            promoteCmd.Parameters.AddWithValue("role", targetRole);
            promoteCmd.Parameters.AddWithValue("attendeeRole", Lookups.UserRoles.PublicViewer);
            promoteCmd.Parameters.AddWithValue("staffRole", Lookups.UserRoles.Staff);
            await promoteCmd.ExecuteNonQueryAsync(ct);

            await using var assignCmd = new NpgsqlCommand(
                "SELECT sp_assign_user_event(@u, @ev, @by, @astart::timestamptz, @aend::timestamptz)", connection);
            assignCmd.Parameters.AddWithValue("u", userId);
            assignCmd.Parameters.AddWithValue("ev", eventId);
            assignCmd.Parameters.AddWithValue("by", (object?)tenantContext.UsersId ?? DBNull.Value);
            assignCmd.Parameters.Add(new NpgsqlParameter("astart", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = DBNull.Value });
            assignCmd.Parameters.Add(new NpgsqlParameter("aend", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = DBNull.Value });
            await assignCmd.ExecuteNonQueryAsync(ct);

            await SendEventAssignmentNotificationAsync(request.Email, eventTitle, startDate, venueName, eventId, targetRole, ct);

            return new AssignStaffByEmailResponse { UserExisted = true, Message = "Team member assigned and notified." };
        }
        else
        {
            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var hash = EmailHasher.Hash(token);
            await using var cmd = new NpgsqlCommand(
                "SELECT sp_create_invitation(@email, @hash, @role, @by, @exp, @t, @event)", connection);
            cmd.Parameters.AddWithValue("role", targetRole);
            cmd.Parameters.AddWithValue("email", request.Email);
            cmd.Parameters.AddWithValue("hash", hash);
            cmd.Parameters.AddWithValue("by", tenantContext.UsersId!);
            var expirySeconds = await settings.GetIntAsync("admin_invitation_expiry", 86400, ct);
            cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddSeconds(expirySeconds));
            cmd.Parameters.AddWithValue("t", tenantContext.TenantsId!);
            cmd.Parameters.AddWithValue("event", eventId);
            await cmd.ExecuteNonQueryAsync(ct);

            await SendInvitationEmailAsync(request.Email, token, expirySeconds, roleName, eventTitle, ct);

            return new AssignStaffByEmailResponse { UserExisted = false, Message = "Invitation sent. Staff member will be assigned once they create an account." };
        }
    }

    public override async Task<AddOrInviteStaffResponse> AddOrInviteStaff(AddOrInviteStaffRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var emailHash = EmailHasher.Hash(request.Email);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        await using var lookup = new NpgsqlCommand(
            "SELECT users_id FROM sp_get_user_by_email_hash(@h) LIMIT 1", connection);
        lookup.Parameters.AddWithValue("h", emailHash);
        var userExistsId = await lookup.ExecuteScalarAsync(ct);

        if (userExistsId is Guid userId)
        {
            await using var promoteCmd = new NpgsqlCommand(
                "SELECT sp_set_user_role(@id, @staffRole, ARRAY[@attendeeRole, @subTenantRole, @eventManagerRole])", connection);
            promoteCmd.Parameters.AddWithValue("id", userId);
            promoteCmd.Parameters.AddWithValue("staffRole", Lookups.UserRoles.Staff);
            promoteCmd.Parameters.AddWithValue("attendeeRole", Lookups.UserRoles.PublicViewer);
            promoteCmd.Parameters.AddWithValue("subTenantRole", Lookups.UserRoles.SubTenant);
            promoteCmd.Parameters.AddWithValue("eventManagerRole", Lookups.UserRoles.EventManager);
            await promoteCmd.ExecuteNonQueryAsync(ct);

            return new AddOrInviteStaffResponse { UserExisted = true, UsersId = userId.ToString(), Message = "Existing user promoted to Staff." };
        }
        else
        {
            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var hash = EmailHasher.Hash(token);
            await using var cmd = new NpgsqlCommand(
                "SELECT sp_create_invitation(@email, @hash, @staffRole, @by, @exp, @t, NULL)", connection);
            cmd.Parameters.AddWithValue("staffRole", Lookups.UserRoles.Staff);
            cmd.Parameters.AddWithValue("email", request.Email);
            cmd.Parameters.AddWithValue("hash", hash);
            cmd.Parameters.AddWithValue("by", tenantContext.UsersId!);
            var expirySeconds = await settings.GetIntAsync("admin_invitation_expiry", 86400, ct);
            cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddSeconds(expirySeconds));
            cmd.Parameters.AddWithValue("t", tenantContext.TenantsId!);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;

            await SendInvitationEmailAsync(request.Email, token, expirySeconds, "Check-in Staff", "", ct);

            return new AddOrInviteStaffResponse { UserExisted = false, UsersId = id.ToString(), Message = "Staff invitation email sent." };
        }
    }

    public override async Task<AckResponse> RemoveStaffRole(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var userId = Guid.Parse(request.Value);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        
        await using var cmd = new NpgsqlCommand("SELECT sp_remove_staff_role(@u, @t, @attendeeRole)", connection);
        cmd.Parameters.AddWithValue("u", userId);
        cmd.Parameters.AddWithValue("t", tenantContext.TenantsId!);
        cmd.Parameters.AddWithValue("attendeeRole", Lookups.UserRoles.PublicViewer);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Staff member removed successfully." };
    }

    private async Task SendEventAssignmentNotificationAsync(
        string recipient,
        string eventTitle,
        DateTime startDate,
        string venueName,
        Guid eventId,
        int targetRole,
        CancellationToken ct)
    {
        var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@ticketspan.com", ct);
        var roleName = targetRole == Lookups.UserRoles.EventManager ? "Event Manager" : "Check-in Staff";
        var subject = $"You have been assigned to {eventTitle}";
        var portalBase = await settings.GetStringAsync("staff_portal_link_base", "http://staff.localhost:5173", ct);
        var portalLink = $"{portalBase.TrimEnd('/')}/staff/{eventId}";

        var values = new Dictionary<string, string>
        {
            ["Subject"] = subject,
            ["Email"] = recipient,
            ["RoleName"] = roleName,
            ["EventTitle"] = eventTitle,
            ["TenantName"] = string.IsNullOrEmpty(tenantContext.TenantSlug) ? "TicketSpan" : tenantContext.TenantSlug,
            ["EventDate"] = startDate.ToString("f"),
            ["VenueName"] = string.IsNullOrEmpty(venueName) ? "Online / Specified Venue" : venueName,
            ["PortalLink"] = portalLink
        };
        var htmlBody = await templates.RenderAsync("staff_event_notification.html", values, ct);
        await emailService.SendAsync(fromAddress, recipient, subject, htmlBody, ct);
    }

    private async Task SendInvitationEmailAsync(
        string recipient,
        string token,
        int expirySeconds,
        string roleName,
        string eventTitle,
        CancellationToken ct)
    {
        var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@ticketspan.com", ct);
        var subject = string.IsNullOrEmpty(eventTitle)
            ? $"You are invited to join TicketSpan as {roleName}"
            : $"You are invited to join as {roleName} for {eventTitle}";
        var linkBase = await settings.GetStringAsync("admin_invitation_link_base", "http://admin.localhost:5173/accept-invitation", ct);
        var separator = linkBase.Contains('?') ? "&" : "?";
        var inviteLink = $"{linkBase}{separator}token={token}";
        var expiryHours = (expirySeconds / 3600).ToString();

        var values = new Dictionary<string, string>
        {
            ["Subject"] = subject,
            ["Email"] = recipient,
            ["RoleName"] = roleName,
            ["InviteLink"] = inviteLink,
            ["ExpiryHours"] = expiryHours,
            ["TenantName"] = string.IsNullOrEmpty(tenantContext.TenantSlug) ? "TicketSpan" : tenantContext.TenantSlug
        };
        var htmlBody = await templates.RenderAsync("admin_invitation.html", values, ct);
        await emailService.SendAsync(fromAddress, recipient, subject, htmlBody, ct);
    }

    private void RequireTenant()
    {
        if (tenantContext.TenantsId is null && !tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
    }
}
