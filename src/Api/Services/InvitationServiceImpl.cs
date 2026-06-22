using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Admin;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class InvitationServiceImpl : InvitationService.InvitationServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public InvitationServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<UuidValue> CreateInvitation(CreateInvitationRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash = EmailHasher.Hash(token);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_invitation(@email, @hash, @role, @by, @exp, @t)", connection);
        cmd.Parameters.AddWithValue("email", request.Email);
        cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("role", (short)request.Role);
        cmd.Parameters.AddWithValue("by", tenantContext.UsersId!);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddDays(7));
        cmd.Parameters.AddWithValue("t", (object?)tenantContext.TenantsId ?? DBNull.Value);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<AckResponse> AcceptInvitation(AcceptInvitationRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var hash = EmailHasher.Hash(request.Token);
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var lookup = new NpgsqlCommand(
            "SELECT invitation_id FROM vw_invitations WHERE token_hash = @h AND status = 'Pending'", connection);
        lookup.Parameters.AddWithValue("h", hash);
        var invitationId = await lookup.ExecuteScalarAsync(ct);
        if (invitationId is not Guid id)
        {
            return new AckResponse { Success = false, Message = "Invalid or expired invitation" };
        }
        await using var cmd = new NpgsqlCommand("SELECT sp_accept_invitation(@id)", connection);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
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
