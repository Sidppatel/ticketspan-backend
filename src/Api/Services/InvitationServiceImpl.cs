using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Admin;
using TicketSpan.Protos.Common;

namespace TicketSpan.Api.Services;

public sealed class InvitationServiceImpl : InvitationService.InvitationServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly AppSettingsProvider settings;
    private readonly IEmailService email;
    private readonly EmailTemplateRenderer templates;
    private readonly PasswordHasher passwordHasher;

    public InvitationServiceImpl(
        Db db,
        TenantContext tenantContext,
        AppSettingsProvider settings,
        IEmailService email,
        EmailTemplateRenderer templates,
        PasswordHasher passwordHasher)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.settings = settings;
        this.email = email;
        this.templates = templates;
        this.passwordHasher = passwordHasher;
    }

    public override async Task<UuidValue> CreateInvitation(CreateInvitationRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash = EmailHasher.Hash(token);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_invitation(@email, @hash, @role, @by, @exp, @t, @event)", connection);
        cmd.Parameters.AddWithValue("email", request.Email);
        cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("role", (short)request.Role);
        cmd.Parameters.AddWithValue("by", tenantContext.UsersId!);
        var expirySeconds = await settings.GetIntAsync("admin_invitation_expiry", 86400, ct);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddSeconds(expirySeconds));
        cmd.Parameters.AddWithValue("t", (object?)tenantContext.TenantsId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("event", string.IsNullOrEmpty(request.EventsId) ? DBNull.Value : Guid.Parse(request.EventsId));
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;

        await SendInvitationEmailAsync(request.Email, token, expirySeconds, ct);

        return new UuidValue { Value = id.ToString() };
    }

    private async Task SendInvitationEmailAsync(string recipient, string token, int expirySeconds, CancellationToken ct)
    {
        var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@ticketspan.com", ct);
        var subject = await settings.GetStringAsync("admin_invitation_subject", "You are invited to join ticketspan", ct);
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
        await email.SendAsync(fromAddress, recipient, subject, htmlBody, ct);
    }

    public override async Task<AckResponse> AcceptInvitation(AcceptInvitationRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var hash = EmailHasher.Hash(request.Token);
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var lookup = new NpgsqlCommand(
            "SELECT invitations_id, email, role, tenants_id, event_id FROM sp_get_invitation_by_token(@h)", connection);
        lookup.Parameters.AddWithValue("h", hash);
        
        Guid id;
        string email;
        short role;
        Guid? tenantsId;
        Guid? eventId;
        
        await using (var reader = await lookup.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
            {
                return new AckResponse { Success = false, Message = "Invalid or expired invitation" };
            }
            id = reader.GetGuid(0);
            email = reader.GetString(1);
            role = reader.GetInt16(2);
            tenantsId = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3);
            eventId = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4);
        }

        var passwordHash = passwordHasher.Hash(request.Password);

        Guid userId;
        await using (var signup = new NpgsqlCommand(
            "SELECT users_id FROM sp_signup_user(@tenant, @email, @hash, @first, @last, @pwd, @pv, @role)", connection))
        {
            signup.Parameters.AddWithValue("tenant", (object?)tenantsId ?? DBNull.Value);
            signup.Parameters.AddWithValue("email", email);
            signup.Parameters.AddWithValue("hash", EmailHasher.Hash(email));
            signup.Parameters.AddWithValue("first", request.FirstName);
            signup.Parameters.AddWithValue("last", request.LastName);
            signup.Parameters.AddWithValue("pwd", passwordHash);
            signup.Parameters.AddWithValue("pv", passwordHasher.CurrentVersion);
            signup.Parameters.AddWithValue("role", role);
            userId = (Guid)(await signup.ExecuteScalarAsync(ct))!;
        }

        await using (var cmd = new NpgsqlCommand("SELECT sp_accept_invitation(@id)", connection))
        {
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        if (eventId.HasValue)
        {
            await using (var assign = new NpgsqlCommand(
                "SELECT sp_assign_user_event(@user, @event, @by)", connection))
            {
                assign.Parameters.AddWithValue("user", userId);
                assign.Parameters.AddWithValue("event", eventId.Value);
                assign.Parameters.AddWithValue("by", DBNull.Value);
                await assign.ExecuteNonQueryAsync(ct);
            }
        }

        return new AckResponse { Success = true, Message = "Invitation accepted" };
    }

    public override async Task<AckResponse> RevokeInvitation(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_revoke_invitation(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Invitation revoked" };
    }

    public override async Task<ListInvitationsResponse> ListInvitations(PageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var response = new ListInvitationsResponse { Meta = new PageMeta() };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT invitation_id, email, role, status, expires_at FROM vw_invitations ORDER BY created_at DESC", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Invitations.Add(new Invitation
            {
                InvitationsId = reader.GetGuid(0).ToString(),
                Email = reader.GetString(1),
                Role = reader.GetInt16(2),
                Status = reader.GetString(3),
                ExpiresAt = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero).ToUnixTimeSeconds()
            });
        }
        response.Meta.Total = response.Invitations.Count;
        return response;
    }

    private void RequireTenant()
    {
        if (tenantContext.TenantsId is null && !tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
    }
}
