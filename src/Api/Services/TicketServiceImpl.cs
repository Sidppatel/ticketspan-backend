using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Purchase;

namespace Svyne.Api.Services;

public sealed class TicketServiceImpl : TicketService.TicketServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public TicketServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<Ticket> GetTicket(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT purchase_ticket_id, ticket_code, qr_token, seat_number, status, guest_users_id "
            + "FROM vw_purchase_tickets WHERE purchase_ticket_id = @id", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Ticket not found"));
        }
        return MapTicket(reader);
    }

    public override async Task<ListTicketsResponse> ListPurchaseTickets(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListTicketsResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT purchase_ticket_id, ticket_code, qr_token, seat_number, status, guest_users_id "
            + "FROM vw_purchase_tickets WHERE purchases_id = @p ORDER BY seat_number", connection);
        cmd.Parameters.AddWithValue("p", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Tickets.Add(MapTicket(reader));
        }
        return response;
    }

    public override async Task<AckResponse> ClaimTicket(ClaimTicketRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
        var hash = EmailHasher.Hash(request.Token);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT success, message FROM sp_claim_ticket_by_token(@h, @u)", connection);
        cmd.Parameters.AddWithValue("h", hash);
        cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new AckResponse { Success = false, Message = "Claim failed" };
        }
        return new AckResponse { Success = reader.GetBoolean(0), Message = reader.IsDBNull(1) ? string.Empty : reader.GetString(1) };
    }

    public override async Task<AckResponse> InviteTicket(InviteTicketRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash = EmailHasher.Hash(token);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_set_ticket_invite(@id, @h, @email, @exp)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.PurchaseTicketsId));
        cmd.Parameters.AddWithValue("h", hash);
        cmd.Parameters.AddWithValue("email", request.Email);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddDays(14));
        var ok = (bool)(await cmd.ExecuteScalarAsync(ct))!;
        return new AckResponse { Success = ok, Message = ok ? "Invite sent" : "Invite failed" };
    }

    private static Ticket MapTicket(NpgsqlDataReader reader) => new()
    {
        PurchaseTicketsId = reader.GetGuid(0).ToString(),
        TicketCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
        QrToken = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
        SeatNumber = reader.GetInt32(3),
        Status = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
        GuestUsersId = reader.IsDBNull(5) ? string.Empty : reader.GetGuid(5).ToString()
    };
}
