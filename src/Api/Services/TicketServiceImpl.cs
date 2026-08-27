using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Booking;

using TicketSpan.Api.Email;

namespace TicketSpan.Api.Services;

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
        var ticketId = Guid.Parse(request.Value);
        var isPublicViewer = tenantContext.Role == Lookups.UserRoles.PublicViewer;
        if (isPublicViewer)
        {
            if (tenantContext.UsersId is null)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
            }
            await using var verifyCmd = new NpgsqlCommand(
                "SELECT 1 FROM vw_tickets WHERE ticket_id = @id AND (guest_users_id = @u OR booking_user_id = @u)", connection);
            verifyCmd.Parameters.AddWithValue("id", ticketId);
            verifyCmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
            if (await verifyCmd.ExecuteScalarAsync(ct) is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Ticket not found"));
            }
        }
        else
        {
            await EventAccess.RequireResolvedAsync(
                connection, tenantContext, "SELECT events_id FROM vw_tickets WHERE ticket_id = @key", ticketId, ct);
        }

        await using var cmd = new NpgsqlCommand(
            "SELECT t.ticket_id, t.ticket_code, t.qr_token, t.seat_number, t.status, t.guest_users_id, "
            + "t.event_title, t.event_start_date, t.venue_name, t.event_slug, t.booking_number, t.ticket_type_label, "
            + "t.invited_email, t.invite_sent_at "
            + "FROM vw_tickets t "
            + "WHERE t.ticket_id = @id", connection);
        cmd.Parameters.AddWithValue("id", ticketId);
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
        var bookingId = Guid.Parse(request.Value);
        var isPublicViewer = tenantContext.Role == Lookups.UserRoles.PublicViewer;
        if (isPublicViewer)
        {
            if (tenantContext.UsersId is null)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
            }
            await using var verifyCmd = new NpgsqlCommand(
                "SELECT 1 FROM vw_bookings WHERE bookings_id = @id AND users_id = @u", connection);
            verifyCmd.Parameters.AddWithValue("id", bookingId);
            verifyCmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
            if (await verifyCmd.ExecuteScalarAsync(ct) is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
            }
        }
        else
        {
            await EventAccess.RequireResolvedAsync(
                connection, tenantContext, "SELECT events_id FROM vw_bookings WHERE bookings_id = @key", bookingId, ct);
        }

        await using var cmd = new NpgsqlCommand(
            "SELECT t.ticket_id, t.ticket_code, t.qr_token, t.seat_number, t.status, t.guest_users_id, "
            + "t.event_title, t.event_start_date, t.venue_name, t.event_slug, t.booking_number, t.ticket_type_label, "
            + "t.invited_email, t.invite_sent_at "
            + "FROM vw_tickets t "
            + "WHERE t.bookings_id = @p ORDER BY t.seat_number", connection);
        cmd.Parameters.AddWithValue("p", bookingId);
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
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
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

        if (ok && !string.IsNullOrWhiteSpace(request.Email))
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
                    var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@ticketspan.com", ct);
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

        return new AckResponse { Success = ok, Message = ok ? token : "Invite failed" };
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
            + "t.event_title, t.event_start_date, t.venue_name, t.event_slug, t.booking_number, t.ticket_type_label "
            + "FROM vw_tickets t "
            + "WHERE t.guest_users_id = @u AND t.status IN ('Claimed', 'CheckedIn') "
            + "ORDER BY t.event_start_date ASC, t.seat_number ASC", connection);
        cmd.Parameters.AddWithValue("u", tenantContext.UsersId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Tickets.Add(MapTicket(reader));
        }
        return response;
    }

    public override async Task<AckResponse> SendTicketEmail(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var ticketId = Guid.Parse(request.Value);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        string recipientEmail = "";
        string ticketCode = "";
        int seatNumber = 0;
        string eventTitle = "";
        DateTime eventStartDate = DateTime.MinValue;
        string venueName = "";

        await using (var cmd = new NpgsqlCommand(
            "SELECT COALESCE(invited_email, guest_email, booking_user_email), ticket_code, seat_number, event_title, event_start_date, venue_name " +
            "FROM vw_tickets " +
            "WHERE ticket_id = @id", connection))
        {
            cmd.Parameters.AddWithValue("id", ticketId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                recipientEmail = reader.IsDBNull(0) ? "" : reader.GetString(0);
                ticketCode = reader.IsDBNull(1) ? "" : reader.GetString(1);
                seatNumber = reader.GetInt32(2);
                eventTitle = reader.IsDBNull(3) ? "" : reader.GetString(3);
                eventStartDate = reader.IsDBNull(4) ? DateTime.UtcNow : reader.GetDateTime(4);
                venueName = reader.IsDBNull(5) ? "Online / Venue" : reader.GetString(5);
            }
        }

        if (string.IsNullOrEmpty(recipientEmail))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "No recipient email found for this ticket"));
        }

        try
        {
            var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@ticketspan.com", ct);
            var subject = $"Your Ticket Pass: {eventTitle}";
            var linkBase = TenantClaimLinkBase();
            var ticketPassLink = $"{linkBase}?ticket={ticketCode}";

            var values = new Dictionary<string, string>
            {
                ["Subject"] = subject,
                ["EventTitle"] = eventTitle,
                ["EventDate"] = eventStartDate.ToString("f"),
                ["VenueName"] = venueName,
                ["TicketCode"] = ticketCode,
                ["SeatNumber"] = seatNumber.ToString(),
                ["TicketPassLink"] = ticketPassLink
            };

            var htmlBody = await templates.RenderAsync("ticket_delivery.html", values, ct);
            await email.SendAsync(fromAddress, recipientEmail, subject, htmlBody, ct);
            logger.LogInformation("Ticket delivery email sent to {Email} for Ticket: {TicketCode}", recipientEmail, ticketCode);
            return new AckResponse { Success = true, Message = "Ticket pass sent to email" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send ticket delivery email to {Email}", recipientEmail);
            return new AckResponse { Success = false, Message = "Failed to send email" };
        }
    }

    public override async Task<ScanResponse> SelfCheckInTicket(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
        if (!Guid.TryParse(request.Value, out var ticketId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ticket ID"));
        }

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        Guid eventId;
        string currentStatus;
        await using (var checkCmd = new NpgsqlCommand(
            "SELECT events_id, status " +
            "FROM vw_tickets " +
            "WHERE ticket_id = @id " +
            "AND (guest_users_id = @u OR (guest_users_id IS NULL AND booking_user_id = @u))", connection))
        {
            checkCmd.Parameters.AddWithValue("id", ticketId);
            checkCmd.Parameters.AddWithValue("u", tenantContext.UsersId);
            await using var reader = await checkCmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Ticket not found or does not belong to user"));
            }
            eventId = reader.GetGuid(0);
            currentStatus = reader.GetString(1);
        }

        if (currentStatus == "CheckedIn")
        {
            return new ScanResponse
            {
                Valid = true,
                Message = "Ticket is already checked in",
                Status = "CheckedIn"
            };
        }

        await using var cmd = new NpgsqlCommand(
            "SELECT success, message, guest_name, status_str FROM sp_check_in_ticket(@id, @ev, @staff, 'universal_pass_self')", connection);
        cmd.Parameters.AddWithValue("id", ticketId);
        cmd.Parameters.AddWithValue("ev", eventId);
        cmd.Parameters.AddWithValue("staff", tenantContext.UsersId);
        await using var resReader = await cmd.ExecuteReaderAsync(ct);
        if (!await resReader.ReadAsync(ct))
        {
            return new ScanResponse { Valid = false, Message = "Self check-in failed" };
        }
        return new ScanResponse
        {
            Valid = resReader.GetBoolean(0),
            Message = resReader.IsDBNull(1) ? "Check-in successful" : resReader.GetString(1),
            HolderName = resReader.IsDBNull(2) ? string.Empty : resReader.GetString(2),
            Status = resReader.IsDBNull(3) ? "CheckedIn" : resReader.GetString(3)
        };
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
