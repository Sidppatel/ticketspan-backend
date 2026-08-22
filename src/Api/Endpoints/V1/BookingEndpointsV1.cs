using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TicketSpan.Api.Endpoints.Common;
using TicketSpan.Api.Services;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Booking;

namespace TicketSpan.Api.Endpoints.V1;

public static class BookingEndpointsV1
{
    public static RouteGroupBuilder MapBookingApiV1(this RouteGroupBuilder group)
    {
        var bookings = group.MapGroup("/bookings").WithTags("Bookings");

        bookings.MapPost("/reserve", async (
            ReserveCapacityApiRequest request,
            BookingServiceImpl bookingService,
            CancellationToken ct) =>
        {
            try
            {
                var req = new ReserveOpenCapacityRequest
                {
                    EventsId = request.EventsId,
                    EventTicketTypesId = request.EventTicketTypesId,
                    Seats = request.Seats,
                    SubtotalCents = request.SubtotalCents,
                    FeeCents = request.FeeCents,
                    TotalCents = request.TotalCents
                };

                var res = await bookingService.ReserveOpenCapacity(req, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<CreateBookingApiResponse>(true, new CreateBookingApiResponse(
                    res.BookingsId,
                    res.BookingNumber)));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: (int)ex.StatusCode switch
                {
                    (int)Grpc.Core.StatusCode.InvalidArgument => StatusCodes.Status400BadRequest,
                    (int)Grpc.Core.StatusCode.ResourceExhausted => StatusCodes.Status409Conflict,
                    (int)Grpc.Core.StatusCode.FailedPrecondition => StatusCodes.Status412PreconditionFailed,
                    _ => StatusCodes.Status500InternalServerError
                });
            }
        }).AllowAnonymous();

        bookings.MapPost("/tables/lock", async (
            LockTableApiRequest request,
            TableBookingServiceImpl tableService,
            CancellationToken ct) =>
        {
            try
            {
                var req = new LockTableRequest
                {
                    TablesId = request.TablesId
                };

                var res = await tableService.LockTable(req, new UnaryServerCallContext(ct));
                return Results.Ok(new AckEnvelope(res.Success, res.Message, res.Code));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: (int)ex.StatusCode switch
                {
                    (int)Grpc.Core.StatusCode.InvalidArgument => StatusCodes.Status400BadRequest,
                    (int)Grpc.Core.StatusCode.ResourceExhausted => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status500InternalServerError
                });
            }
        }).AllowAnonymous();

        bookings.MapPost("/tables/release", async (
            ReleaseTableLockApiRequest request,
            TableBookingServiceImpl tableService,
            CancellationToken ct) =>
        {
            try
            {
                var req = new LockTableRequest
                {
                    TablesId = request.TablesId
                };

                var res = await tableService.ReleaseTableLock(req, new UnaryServerCallContext(ct));
                return Results.Ok(new AckEnvelope(res.Success, res.Message, res.Code));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        bookings.MapPost("/payment-intent", async (
            CreatePaymentIntentApiRequest request,
            BookingServiceImpl bookingService,
            CancellationToken ct) =>
        {
            try
            {
                var req = new PaymentIntentRequest
                {
                    BookingsId = request.BookingsId,
                    PreferredMethod = request.PreferredMethod ?? string.Empty
                };

                var res = await bookingService.CreatePaymentIntent(req, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<PaymentIntentApiResponse>(true, new PaymentIntentApiResponse(
                    res.ClientSecret,
                    res.PublishableKey,
                    res.PaymentIntentId,
                    res.Status,
                    res.AmountCents,
                    res.HoldExpiresAt,
                    res.AchAllowed)));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: (int)ex.StatusCode switch
                {
                    (int)Grpc.Core.StatusCode.InvalidArgument => StatusCodes.Status400BadRequest,
                    (int)Grpc.Core.StatusCode.FailedPrecondition => StatusCodes.Status412PreconditionFailed,
                    _ => StatusCodes.Status500InternalServerError
                });
            }
        }).AllowAnonymous();

        bookings.MapGet("/{bookingId}", async (
            string bookingId,
            BookingServiceImpl bookingService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(bookingId, out _))
            {
                return Results.BadRequest(new ApiEnvelope<BookingDetailApiResponse>(false, null, "Invalid booking ID"));
            }

            try
            {
                var res = await bookingService.GetBooking(new UuidValue { Value = bookingId }, new UnaryServerCallContext(ct));
                var mapped = new BookingDetailApiResponse(
                    res.BookingsId,
                    res.TenantsId,
                    res.BookingNumber,
                    res.Status,
                    res.UsersId,
                    res.EventsId,
                    res.SubtotalCents,
                    res.FeeCents,
                    res.TotalCents,
                    res.SeatsReserved,
                    res.EventTitle,
                    res.EventSlug,
                    res.EventStartDate,
                    res.TicketsTotal,
                    res.TicketsClaimed,
                    res.PaymentTransactionId,
                    res.FeesIncluded,
                    res.VenueName,
                    res.VenueAddress,
                    res.PaidAt,
                    res.TaxCents,
                    res.ServiceFeeCents,
                    res.VenueZip,
                    res.VenueCity,
                    res.VenueState,
                    res.PaymentMethodType,
                    res.PaymentMethodLast4,
                    res.PaymentMethodBrand,
                    res.UserEmail,
                    res.UserName,
                    res.Lines.Select(l => new BookingLineDto(
                        l.BookingLinesId,
                        l.Kind,
                        l.Label,
                        l.EventTicketTypesId,
                        l.TablesId,
                        l.Seats,
                        l.SubtotalCents,
                        l.FeeCents,
                        l.TotalCents,
                        l.BasePriceCents,
                        l.SellingPriceCents,
                        l.DiscountCents,
                        l.AppliedRuleName,
                        l.PlatformFeeCents,
                        l.GatewayFeeCents,
                        l.TaxCents,
                        l.FinalPriceCents,
                        l.Currency)).ToList());

                return Results.Ok(new ApiEnvelope<BookingDetailApiResponse>(true, mapped));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                return Results.NotFound(new ApiEnvelope<BookingDetailApiResponse>(false, null, "Booking not found"));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        return group;
    }
}

public sealed record ReserveCapacityApiRequest(
    string EventsId,
    string EventTicketTypesId,
    int Seats,
    int SubtotalCents,
    int FeeCents,
    int TotalCents);

public sealed record CreateBookingApiResponse(string BookingsId, string BookingNumber);

public sealed record LockTableApiRequest(string TablesId);
public sealed record ReleaseTableLockApiRequest(string TablesId);

public sealed record CreatePaymentIntentApiRequest(string BookingsId, string? PreferredMethod);

public sealed record PaymentIntentApiResponse(
    string ClientSecret,
    string PublishableKey,
    string PaymentIntentId,
    string Status,
    long AmountCents,
    long HoldExpiresAt,
    bool AchAllowed);

public sealed record BookingDetailApiResponse(
    string BookingsId,
    string TenantsId,
    string BookingNumber,
    string Status,
    string UsersId,
    string EventsId,
    int SubtotalCents,
    int FeeCents,
    int TotalCents,
    int SeatsReserved,
    string EventTitle,
    string EventSlug,
    long EventStartDate,
    int TicketsTotal,
    int TicketsClaimed,
    string PaymentTransactionId,
    bool FeesIncluded,
    string VenueName,
    string VenueAddress,
    long PaidAt,
    int TaxCents,
    int ServiceFeeCents,
    string VenueZip,
    string VenueCity,
    string VenueState,
    string PaymentMethodType,
    string PaymentMethodLast4,
    string PaymentMethodBrand,
    string UserEmail,
    string UserName,
    IReadOnlyList<BookingLineDto> Lines);

public sealed record BookingLineDto(
    string BookingLinesId,
    string Kind,
    string Label,
    string EventTicketTypesId,
    string TablesId,
    int Seats,
    int SubtotalCents,
    int FeeCents,
    int TotalCents,
    int BasePriceCents,
    int SellingPriceCents,
    int DiscountCents,
    string AppliedRuleName,
    int PlatformFeeCents,
    int GatewayFeeCents,
    int TaxCents,
    int FinalPriceCents,
    string Currency);
