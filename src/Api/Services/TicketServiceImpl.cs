using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Booking;

using Svyne.Api.Email;

namespace Svyne.Api.Services;

public sealed class TicketServiceImpl : TicketService.TicketServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly AppSettingsProvider settings;
    private readonly IEmailService email;
    private readonly EmailTemplateRenderer templates;
    private readonly ILogger<TicketServiceImpl> logger;
    private readonly IConfiguration configuration;

    public TicketServiceImpl(
        Db db,
        TenantContext tenantContext,
        AppSettingsProvider settings,
        IEmailService email,
        EmailTemplateRenderer templates,
        ILogger<TicketServiceImpl> logger,
        IConfiguration configuration)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.settings = settings;
        this.email = email;
        this.templates = templates;
        this.logger = logger;
        this.configuration = configuration;
    }

    private string TenantClaimLinkBase()
    {
        var adminUrl = configuration["FRONTEND_ADMIN_URL"]?.TrimEnd('/') ?? "http://admin.localhost:5173";
        var uri = new Uri(adminUrl);
        var host = uri.Host.StartsWith("admin.") && !string.IsNullOrEmpty(tenantContext.TenantSlug)
            ? tenantContext.TenantSlug + uri.Host["admin".Length..]
            : uri.Host;
        var port = uri.IsDefaultPort ? "" : ":" + uri.Port;
        return $"{uri.Scheme}://{host}{port}/claim";
    }

    public override async Task<Ticket> GetTicket(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await EventAccess.RequireResolvedAsync(
            connection, tenantContext, "SELECT events_id FROM vw_tickets WHERE ticket_id = @key", Guid.Parse(request.Value), ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT t.ticket_id, t.ticket_code, t.qr_token, t.seat_number, t.status, t.guest_users_id, "
            + "t.event_title, t.event_start_date, t.venue_name, e.slug AS event_slug, t.booking_number, t.ticket_type_label, "
            + "t.invited_email, t.invite_sent_at "
            + "FROM vw_tickets t "
            + "JOIN events e ON t.events_id = e.events_id "
            + "WHERE t.ticket_id = @id", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Ticket not found"));
        }
        return MapTicket(reader);
    }

    public override async Task<ListTicketsResponse> ListTickets(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListTicketsResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await EventAccess.RequireResolvedAsync(
            connection, tenantContext, "SELECT events_id FROM bookings WHERE bookings_id = @key", Guid.Parse(request.Value), ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT t.ticket_id, t.ticket_code, t.qr_token, t.seat_number, t.status, t.guest_users_id, "
            + "t.event_title, t.event_start_date, t.venue_name, e.slug AS event_slug, t.booking_number, t.ticket_type_label, "
            + "t.invited_email, t.invite_sent_at "
            + "FROM vw_tickets t "
            + "JOIN events e ON t.events_id = e.events_id "
            + "WHERE t.bookings_id = @p ORDER BY t.seat_number", connection);
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
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.TicketsId));
        cmd.Parameters.AddWithValue("h", hash);
        cmd.Parameters.AddWithValue("email", request.Email);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddDays(14));
        var ok = (bool)(await cmd.ExecuteScalarAsync(ct))!;

        if (ok)
        {
            try
            {
                await using var detailCmd = new NpgsqlCommand(
                    "SELECT ticket_code, seat_number, event_title, event_start_date, venue_name, booking_user_email " +
                    "FROM vw_tickets WHERE ticket_id = @id", connection);
                detailCmd.Parameters.AddWithValue("id", Guid.Parse(request.TicketsId));
                
                string ticketCode = "";
                int seatNumber = 0;
                string eventTitle = "";
                DateTime eventStartDate = DateTime.MinValue;
                string venueName = "";
                string senderEmail = "";
                
                await using (var reader = await detailCmd.ExecuteReaderAsync(ct))
                {
                    if (await reader.ReadAsync(ct))
                    {
                        ticketCode = reader.GetString(0);
                        seatNumber = reader.GetInt32(1);
                        eventTitle = reader.GetString(2);
                        eventStartDate = reader.GetDateTime(3);
                        venueName = reader.GetString(4);
                        senderEmail = reader.GetString(5);
                    }
                }

                if (!string.IsNullOrEmpty(ticketCode))
                {
                    var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@svyne.com", ct);
                    var subject = $"You have been invited to {eventTitle}!";
                    var linkBase = await settings.GetStringAsync("ticket_claim_link_base", "", ct);
                    if (string.IsNullOrEmpty(linkBase))
                    {
                        linkBase = TenantClaimLinkBase();
                    }
                    var separator = linkBase.Contains('?') ? "&" : "?";
                    var claimLink = $"{linkBase}{separator}token={token}";

                    var values = new Dictionary<string, string>
                    {
                        ["Subject"] = subject,
                        ["Email"] = request.Email,
                        ["SenderEmail"] = senderEmail,
                        ["EventTitle"] = eventTitle,
                        ["EventDate"] = eventStartDate.ToString("f"),
                        ["VenueName"] = venueName,
                        ["TicketCode"] = ticketCode,
                        ["SeatNumber"] = seatNumber.ToString(),
                        ["InviteLink"] = claimLink
                    };

                    var htmlBody = await templates.RenderAsync("ticket_invitation.html", values, ct);
                    await email.SendAsync(fromAddress, request.Email, subject, htmlBody, ct);
                    logger.LogInformation("Ticket invitation email sent to {Email} for Ticket: {TicketCode}", request.Email, ticketCode);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send ticket invitation email to {Email}", request.Email);
            }
        }

        return new AckResponse { Success = ok, Message = ok ? "Invite sent" : "Invite failed" };
    }

    public override async Task<AckResponse> ClaimTicketSelf(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT success, message FROM sp_claim_ticket_self(@ticket_id, @user_id)", connection);
        cmd.Parameters.AddWithValue("ticket_id", Guid.Parse(request.Value));
        cmd.Parameters.AddWithValue("user_id", tenantContext.UsersId!);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new AckResponse { Success = false, Message = "Claim failed" };
        }
        return new AckResponse { Success = reader.GetBoolean(0), Message = reader.IsDBNull(1) ? string.Empty : reader.GetString(1) };
    }

    public override async Task<AckResponse> RevokeTicket(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_revoke_ticket_invite(@ticket_id)", connection);
        cmd.Parameters.AddWithValue("ticket_id", Guid.Parse(request.Value));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Ticket revoked" };
    }

    public override async Task<ListTicketsResponse> ListMyTickets(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
        var response = new ListTicketsResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT t.ticket_id, t.ticket_code, t.qr_token, t.seat_number, t.status, t.guest_users_id, "
            + "t.event_title, t.event_start_date, t.venue_name, e.slug AS event_slug, t.booking_number, t.ticket_type_label "
            + "FROM vw_tickets t "
            + "JOIN events e ON t.events_id = e.events_id "
            + "WHERE t.guest_users_id = @u "
            + "ORDER BY t.event_start_date", connection);
        cmd.Parameters.AddWithValue("u", tenantContext.UsersId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Tickets.Add(MapTicket(reader));
        }
        return response;
    }

    private static Ticket MapTicket(NpgsqlDataReader reader)
    {
        var ticket = new Ticket
        {
            TicketsId = reader.GetGuid(0).ToString(),
            TicketCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            QrToken = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            SeatNumber = reader.GetInt32(3),
            Status = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            GuestUsersId = reader.IsDBNull(5) ? string.Empty : reader.GetGuid(5).ToString()
        };
        if (reader.FieldCount > 6)
        {
            ticket.EventTitle = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
            ticket.EventStartDate = reader.IsDBNull(7) ? 0 : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc)).ToUnixTimeSeconds();
            ticket.VenueName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
            ticket.EventSlug = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
            if (reader.FieldCount > 10)
            {
                ticket.BookingNumber = reader.IsDBNull(10) ? string.Empty : reader.GetString(10);
            }
            if (reader.FieldCount > 11)
            {
                ticket.TicketTypeLabel = reader.IsDBNull(11) ? string.Empty : reader.GetString(11);
            }
            if (reader.FieldCount > 13)
            {
                ticket.InvitedEmail = reader.IsDBNull(12) ? string.Empty : reader.GetString(12);
                ticket.InviteSentAt = reader.IsDBNull(13) ? 0 : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc)).ToUnixTimeSeconds();
            }
        }
        return ticket;
    }
}
