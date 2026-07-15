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
            "SELECT users_id, first_name, last_name, email, user_role FROM sp_list_staff_for_event(@ev)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Staff.Add(new StaffMember
            {
                UsersId = reader.GetGuid(0).ToString(),
                FirstName = reader.GetString(1),
                LastName = reader.GetString(2),
                Email = reader.GetString(3),
                Role = reader.GetInt32(4)
            });
        }
        return response;
    }

    public override async Task<AckResponse> AssignStaff(AssignStaffRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_assign_user_event(@u, @ev, @by)", connection);
        cmd.Parameters.AddWithValue("u", Guid.Parse(request.UsersId));
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("by", (object?)tenantContext.UsersId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Staff assigned" };
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
        await using var cmd = new NpgsqlCommand("SELECT users_id, email, display_name FROM sp_get_tenant_members(@t) WHERE role = @staffRole", connection);
        cmd.Parameters.AddWithValue("t", tenantContext.TenantsId!);
        cmd.Parameters.AddWithValue("staffRole", Lookups.UserRoles.Staff);
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
                Email = reader.GetString(1)
            });
        }
        return response;
    }

    public override async Task<AssignStaffByEmailResponse> AssignStaffByEmail(AssignStaffByEmailRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var targetRole = request.Role == Lookups.UserRoles.EventManager ? Lookups.UserRoles.EventManager : Lookups.UserRoles.Staff;
        var emailHash = EmailHasher.Hash(request.Email);
        var eventId = Guid.Parse(request.EventsId);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        await using var lookup = new NpgsqlCommand(
            "SELECT users_id FROM sp_get_user_by_email_hash(@h) WHERE tenants_id = @t", connection);
        lookup.Parameters.AddWithValue("h", emailHash);
        lookup.Parameters.AddWithValue("t", tenantContext.TenantsId!);
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
                "SELECT sp_assign_user_event(@u, @ev, @by)", connection);
            assignCmd.Parameters.AddWithValue("u", userId);
            assignCmd.Parameters.AddWithValue("ev", eventId);
            assignCmd.Parameters.AddWithValue("by", (object?)tenantContext.UsersId ?? DBNull.Value);
            await assignCmd.ExecuteNonQueryAsync(ct);

            return new AssignStaffByEmailResponse { UserExisted = true, Message = "Team member assigned successfully." };
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

            await SendInvitationEmailAsync(request.Email, token, expirySeconds, ct);

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
            "SELECT users_id FROM sp_get_user_by_email_hash(@h) WHERE tenants_id = @t", connection);
        lookup.Parameters.AddWithValue("h", emailHash);
        lookup.Parameters.AddWithValue("t", tenantContext.TenantsId!);
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

            await SendInvitationEmailAsync(request.Email, token, expirySeconds, ct);

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

    private async Task SendInvitationEmailAsync(string recipient, string token, int expirySeconds, CancellationToken ct)
    {
        var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@ticketspan.com", ct);
        var subject = await settings.GetStringAsync("admin_invitation_subject", "You are invited to join ticketspan as staff", ct);
        var linkBase = await settings.GetStringAsync("admin_invitation_link_base", "http://admin.localhost:5173/accept-invitation", ct);
        var separator = linkBase.Contains('?') ? "&" : "?";
        var inviteLink = $"{linkBase}{separator}token={token}";
        var expiryHours = (expirySeconds / 3600).ToString();

        var values = new Dictionary<string, string>
        {
            ["Subject"] = subject,
            ["Email"] = recipient,
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
