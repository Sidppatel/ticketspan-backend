using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TicketSpan.Api.Endpoints.Common;
using TicketSpan.Api.Services;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Booking;

namespace TicketSpan.Api.Endpoints.V1;

public static class CheckInEndpointsV1
{
    public static RouteGroupBuilder MapCheckInApiV1(this RouteGroupBuilder group)
    {
        var checkin = group.MapGroup("/checkin").WithTags("CheckIn");

        checkin.MapPost("/scan", async (
            ScanTicketApiRequest request,
            CheckInServiceImpl checkInService,
            CancellationToken ct) =>
        {
            try
            {
                var req = new ScanRequest
                {
                    EventsId = request.EventsId,
                    QrToken = request.QrToken
                };

                var res = await checkInService.Scan(req, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<ScanTicketApiResponse>(true, new ScanTicketApiResponse(
                    res.Valid,
                    res.Message,
                    res.HolderName,
                    res.Status)));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
            {
                return Results.Unauthorized();
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.PermissionDenied)
            {
                return Results.Forbid();
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        checkin.MapPost("/manual", async (
            ManualCheckInApiRequest request,
            CheckInServiceImpl checkInService,
            CancellationToken ct) =>
        {
            try
            {
                var req = new CheckInGuestRequest
                {
                    EventsId = request.EventsId,
                    CodeOrId = request.CodeOrId,
                    Type = request.Type ?? "ticket_id"
                };

                var res = await checkInService.CheckInGuest(req, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<ScanTicketApiResponse>(true, new ScanTicketApiResponse(
                    res.Valid,
                    res.Message,
                    res.HolderName,
                    res.Status)));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
            {
                return Results.Unauthorized();
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.PermissionDenied)
            {
                return Results.Forbid();
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        checkin.MapGet("/events", async (
            CheckInServiceImpl checkInService,
            CancellationToken ct) =>
        {
            try
            {
                var res = await checkInService.ListEventsForStaff(new Empty(), new UnaryServerCallContext(ct));
                var items = res.Events.Select(e => new StaffAssignedEventDto(
                    e.EventsId,
                    e.Title,
                    e.Slug,
                    e.StartDate,
                    e.EndDate,
                    e.Status,
                    e.VenueName)).ToList();

                return Results.Ok(new ApiEnvelope<IReadOnlyList<StaffAssignedEventDto>>(true, items));
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

        checkin.MapGet("/guest-list/{eventId}", async (
            string eventId,
            CheckInServiceImpl checkInService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(eventId, out _))
            {
                return Results.BadRequest(new ApiEnvelope<IReadOnlyList<GuestBookingDto>>(false, null, "Invalid event ID"));
            }

            try
            {
                var res = await checkInService.GetGuestList(new UuidValue { Value = eventId }, new UnaryServerCallContext(ct));
                var items = res.Bookings.Select(b => new GuestBookingDto(
                    b.BookingsId,
                    b.BookingNumber,
                    b.BuyerName,
                    b.Status,
                    b.Tickets.Select(t => new GuestTicketDto(
                        t.TicketsId,
                        t.TicketCode,
                        t.GuestName,
                        t.Status,
                        t.SeatNumber,
                        t.CheckedInAt)).ToList())).ToList();

                return Results.Ok(new ApiEnvelope<IReadOnlyList<GuestBookingDto>>(true, items));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
            {
                return Results.Unauthorized();
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.PermissionDenied)
            {
                return Results.Forbid();
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        checkin.MapGet("/stats/{eventId}", async (
            string eventId,
            CheckInServiceImpl checkInService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(eventId, out _))
            {
                return Results.BadRequest(new ApiEnvelope<CheckInStatsApiResponse>(false, null, "Invalid event ID"));
            }

            try
            {
                var res = await checkInService.GetCheckInStats(new UuidValue { Value = eventId }, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<CheckInStatsApiResponse>(true, new CheckInStatsApiResponse(
                    res.Total,
                    res.CheckedIn,
                    res.Remaining)));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
            {
                return Results.Unauthorized();
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.PermissionDenied)
            {
                return Results.Forbid();
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        return group;
    }
}

public sealed record ScanTicketApiRequest(string EventsId, string QrToken);
public sealed record ScanTicketApiResponse(bool Valid, string Message, string HolderName, string Status);

public sealed record ManualCheckInApiRequest(string EventsId, string CodeOrId, string? Type);

public sealed record CheckInStatsApiResponse(int Total, int CheckedIn, int Remaining);

public sealed record StaffAssignedEventDto(
    string EventsId,
    string Title,
    string Slug,
    long StartDate,
    long EndDate,
    string Status,
    string VenueName);

public sealed record GuestBookingDto(
    string BookingsId,
    string BookingNumber,
    string BuyerName,
    string Status,
    IReadOnlyList<GuestTicketDto> Tickets);

public sealed record GuestTicketDto(
    string TicketsId,
    string TicketCode,
    string GuestName,
    string Status,
    int SeatNumber,
    long CheckedInAt);
