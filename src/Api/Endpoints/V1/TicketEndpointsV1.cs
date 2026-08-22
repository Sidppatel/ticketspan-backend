using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TicketSpan.Api.Endpoints.Common;
using TicketSpan.Api.Services;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Booking;

namespace TicketSpan.Api.Endpoints.V1;

public static class TicketEndpointsV1
{
    public static RouteGroupBuilder MapTicketApiV1(this RouteGroupBuilder group)
    {
        var tickets = group.MapGroup("/tickets").WithTags("Tickets");

        tickets.MapGet("/my", async (
            TicketServiceImpl ticketService,
            CancellationToken ct) =>
        {
            try
            {
                var res = await ticketService.ListMyTickets(new Empty(), new UnaryServerCallContext(ct));
                var items = res.Tickets.Select(MapTicketDto).ToList();
                return Results.Ok(new ApiEnvelope<IReadOnlyList<DigitalTicketDto>>(true, items));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
            {
                return Results.Unauthorized();
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        tickets.MapGet("/{ticketId}", async (
            string ticketId,
            TicketServiceImpl ticketService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(ticketId, out _))
            {
                return Results.BadRequest(new ApiEnvelope<DigitalTicketDto>(false, null, "Invalid ticket ID"));
            }

            try
            {
                var res = await ticketService.GetTicket(new UuidValue { Value = ticketId }, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<DigitalTicketDto>(true, MapTicketDto(res)));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                return Results.NotFound(new ApiEnvelope<DigitalTicketDto>(false, null, "Ticket not found"));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.PermissionDenied)
            {
                return Results.Forbid();
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        tickets.MapPost("/{ticketId}/claim", async (
            string ticketId,
            TicketServiceImpl ticketService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(ticketId, out _))
            {
                return Results.BadRequest(new AckEnvelope(false, "Invalid ticket ID", 400));
            }

            try
            {
                var res = await ticketService.ClaimTicketSelf(new UuidValue { Value = ticketId }, new UnaryServerCallContext(ct));
                return Results.Ok(new AckEnvelope(res.Success, res.Message, res.Code));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
            {
                return Results.Unauthorized();
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        tickets.MapPost("/invite", async (
            InviteTicketApiRequest request,
            TicketServiceImpl ticketService,
            CancellationToken ct) =>
        {
            try
            {
                var req = new InviteTicketRequest
                {
                    TicketsId = request.TicketsId,
                    Email = request.Email
                };

                var res = await ticketService.InviteTicket(req, new UnaryServerCallContext(ct));
                return Results.Ok(new AckEnvelope(res.Success, res.Message, res.Code));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
            {
                return Results.Unauthorized();
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        return group;
    }

    private static DigitalTicketDto MapTicketDto(Ticket t) =>
        new(
            t.TicketsId,
            t.TicketCode,
            t.QrToken,
            t.SeatNumber,
            t.Status,
            t.GuestUsersId,
            t.EventTitle,
            t.EventStartDate,
            t.VenueName,
            t.EventSlug,
            t.BookingNumber,
            t.TicketTypeLabel,
            t.InvitedEmail,
            t.InviteSentAt);
}

public sealed record InviteTicketApiRequest(string TicketsId, string Email);

public sealed record DigitalTicketDto(
    string TicketsId,
    string TicketCode,
    string QrToken,
    int SeatNumber,
    string Status,
    string GuestUsersId,
    string EventTitle,
    long EventStartDate,
    string VenueName,
    string EventSlug,
    string BookingNumber,
    string TicketTypeLabel,
    string InvitedEmail,
    long InviteSentAt);
