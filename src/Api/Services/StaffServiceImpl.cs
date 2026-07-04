using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Api.Email;
using Svyne.Protos.Admin;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

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
        await using var cmd = new NpgsqlCommand("SELECT users_id, email, display_name FROM sp_get_tenant_members(@t) WHERE role = 2", connection);
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
                Email = reader.GetString(1)
            });
        }
        return response;
    }

    public override async Task<AssignStaffByEmailResponse> AssignStaffByEmail(AssignStaffByEmailRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        // Event managers (role 4) get scoped admin access to assigned events; anything
        // else is a check-in staff (role 2). Never let an arbitrary role value through.
        var targetRole = request.Role == 4 ? 4 : 2;
        var emailHash = EmailHasher.Hash(request.Email);
        var eventId = Guid.Parse(request.EventsId);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        // Check if user exists
        await using var lookup = new NpgsqlCommand(
            "SELECT users_id FROM users WHERE email_hash = @h AND tenants_id = @t", connection);
        lookup.Parameters.AddWithValue("h", emailHash);
        lookup.Parameters.AddWithValue("t", tenantContext.TenantsId!);
        var userExistsId = await lookup.ExecuteScalarAsync(ct);

        if (userExistsId is Guid userId)
        {
            // Promote attendees and check-in staff up to the requested scoped role; never
            // touch an existing admin (1) / developer (99).
            await using var promoteCmd = new NpgsqlCommand(
                "UPDATE users SET role = @role WHERE users_id = @id AND role IN (0, 2)", connection);
            promoteCmd.Parameters.AddWithValue("id", userId);
            promoteCmd.Parameters.AddWithValue("role", targetRole);
            await promoteCmd.ExecuteNonQueryAsync(ct);

            // Assign event access
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
            // User does not exist, create invitation with linked event
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

        // Check if user exists
        await using var lookup = new NpgsqlCommand(
            "SELECT users_id FROM users WHERE email_hash = @h AND tenants_id = @t", connection);
        lookup.Parameters.AddWithValue("h", emailHash);
        lookup.Parameters.AddWithValue("t", tenantContext.TenantsId!);
        var userExistsId = await lookup.ExecuteScalarAsync(ct);

        if (userExistsId is Guid userId)
        {
            // Promote role to 2 (Staff) if they aren't admin/developer/staff
            await using var promoteCmd = new NpgsqlCommand(
                "UPDATE users SET role = 2 WHERE users_id = @id AND role NOT IN (1, 2, 99)", connection);
            promoteCmd.Parameters.AddWithValue("id", userId);
            await promoteCmd.ExecuteNonQueryAsync(ct);

            return new AddOrInviteStaffResponse { UserExisted = true, UsersId = userId.ToString(), Message = "Existing user promoted to Staff." };
        }
        else
        {
            // Create invitation
            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var hash = EmailHasher.Hash(token);
            await using var cmd = new NpgsqlCommand(
                "SELECT sp_create_invitation(@email, @hash, 2, @by, @exp, @t, NULL)", connection);
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
        
        // Delete event assignments
        await using (var deleteCmd = new NpgsqlCommand("DELETE FROM staff_event_access WHERE staff_user_id = @u", connection))
        {
            deleteCmd.Parameters.AddWithValue("u", userId);
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }
        
        // Reset role to 0 (Attendee)
        await using (var roleCmd = new NpgsqlCommand("UPDATE users SET role = 0 WHERE users_id = @u AND tenants_id = @t", connection))
        {
            roleCmd.Parameters.AddWithValue("u", userId);
            roleCmd.Parameters.AddWithValue("t", tenantContext.TenantsId!);
            await roleCmd.ExecuteNonQueryAsync(ct);
        }
        return new AckResponse { Success = true, Message = "Staff member removed successfully." };
    }

    private async Task SendInvitationEmailAsync(string recipient, string token, int expirySeconds, CancellationToken ct)
    {
        var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@svyne.com", ct);
        var subject = await settings.GetStringAsync("admin_invitation_subject", "You are invited to join svyne as staff", ct);
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
            ["TenantName"] = string.IsNullOrEmpty(tenantContext.TenantSlug) ? "Svyne" : tenantContext.TenantSlug
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
