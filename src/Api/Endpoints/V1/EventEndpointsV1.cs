using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TicketSpan.Api.Endpoints.Common;
using TicketSpan.Api.Services;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Event;
using TicketSpan.Protos.Booking;

namespace TicketSpan.Api.Endpoints.V1;

public static class EventEndpointsV1
{
    public static RouteGroupBuilder MapEventApiV1(this RouteGroupBuilder group)
    {
        var events = group.MapGroup("/events").WithTags("Events");

        events.MapGet("/", async (
            string? category,
            string? search,
            int? page,
            int? pageSize,
            EventServiceImpl eventService,
            CancellationToken ct) =>
        {
            var p = page is > 0 ? page.Value : 1;
            var ps = pageSize is > 0 and <= 100 ? pageSize.Value : 20;

            try
            {
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchRes = await eventService.SearchEvents(new SearchEventsRequest
                    {
                        Query = search
                    }, new UnaryServerCallContext(ct));

                    var items = searchRes.Events.Select(MapEventDto).ToList();
                    var total = searchRes.Meta?.Total ?? items.Count;
                    return Results.Ok(new PagedEnvelope<EventSummaryDto>(true, items, total, p, ps));
                }

                var listRes = await eventService.ListEvents(new ListEventsRequest
                {
                    Category = category ?? string.Empty,
                    Page = new PageRequest { Offset = (p - 1) * ps, Limit = ps }
                }, new UnaryServerCallContext(ct));

                var mapped = listRes.Events.Select(MapEventDto).ToList();
                var totalCount = listRes.Meta?.Total ?? mapped.Count;
                return Results.Ok(new PagedEnvelope<EventSummaryDto>(true, mapped, totalCount, p, ps));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        events.MapGet("/{slugOrId}", async (
            string slugOrId,
            EventServiceImpl eventService,
            CancellationToken ct) =>
        {
            try
            {
                if (Guid.TryParse(slugOrId, out _))
                {
                    var res = await eventService.GetEvent(new UuidValue { Value = slugOrId }, new UnaryServerCallContext(ct));
                    return Results.Ok(new ApiEnvelope<EventDetailDto>(true, MapEventDetailDto(res)));
                }

                var slugRes = await eventService.GetEventBySlug(new GetEventBySlugRequest
                {
                    Slug = slugOrId
                }, new UnaryServerCallContext(ct));

                return Results.Ok(new ApiEnvelope<EventDetailDto>(true, MapEventDetailDto(slugRes)));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                return Results.NotFound(new ApiEnvelope<EventDetailDto>(false, null, "Event not found"));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        events.MapGet("/{id}/ticket-types", async (
            string id,
            BookingServiceImpl bookingService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out _))
            {
                return Results.BadRequest(new ApiEnvelope<IReadOnlyList<TicketTypeDto>>(false, null, "Invalid event ID"));
            }

            try
            {
                var res = await bookingService.ListEventTicketTypes(new UuidValue { Value = id }, new UnaryServerCallContext(ct));
                var items = res.TicketTypes.Select(t => new TicketTypeDto(
                    t.EventTicketTypesId,
                    t.Label,
                    t.PriceCents,
                    t.SellingPriceCents,
                    t.Capacity,
                    t.SoldCount,
                    t.MaxQuantity,
                    t.Description,
                    t.ServiceFeeCents,
                    t.TaxCents,
                    t.TotalCents)).ToList();

                return Results.Ok(new ApiEnvelope<IReadOnlyList<TicketTypeDto>>(true, items));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                return Results.NotFound(new ApiEnvelope<IReadOnlyList<TicketTypeDto>>(false, null, "Event ticket types not found"));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        events.MapGet("/{id}/tables", async (
            string id,
            TableBookingServiceImpl tableService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out _))
            {
                return Results.BadRequest(new ApiEnvelope<IReadOnlyList<TableDto>>(false, null, "Invalid event ID"));
            }

            try
            {
                var res = await tableService.ListTablesForEvent(new UuidValue { Value = id }, new UnaryServerCallContext(ct));
                var items = res.Tables.Select(t => new TableDto(
                    t.TablesId,
                    t.EventTablesId,
                    t.Label,
                    t.CapacityOverride,
                    t.PriceCents,
                    t.Status,
                    t.PosX,
                    t.PosY,
                    t.Width,
                    t.Height,
                    t.ShapeOverride,
                    t.ColorOverride)).ToList();

                return Results.Ok(new ApiEnvelope<IReadOnlyList<TableDto>>(true, items));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        events.MapGet("/{id}/schedule", async (
            string id,
            EventServiceImpl eventService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out _))
            {
                return Results.BadRequest(new ApiEnvelope<IReadOnlyList<ScheduleItemDto>>(false, null, "Invalid event ID"));
            }

            try
            {
                var res = await eventService.ListScheduleItems(new UuidValue { Value = id }, new UnaryServerCallContext(ct));
                var items = res.Items.Select(s => new ScheduleItemDto(
                    s.ScheduleItemsId,
                    s.EventsId,
                    s.Title,
                    s.TypeCategory,
                    s.StartTime,
                    s.EndTime)).ToList();

                return Results.Ok(new ApiEnvelope<IReadOnlyList<ScheduleItemDto>>(true, items));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        return group;
    }

    private static EventSummaryDto MapEventDto(TicketSpan.Protos.Event.Event e) =>
        new(
            e.EventsId,
            e.TenantsId,
            e.Title,
            e.Slug,
            e.Description,
            e.ShortDescription,
            e.Status,
            e.Category,
            e.StartDate,
            e.EndDate,
            e.ImagePath,
            e.HeroBackdropImageId,
            e.PosterImageId,
            e.IsFeatured,
            e.LayoutMode,
            e.EventType,
            e.VenuesId,
            e.TotalCapacity,
            e.IsVerifiedOrganizer,
            e.UrgencyBadgeText);

    private static EventDetailDto MapEventDetailDto(TicketSpan.Protos.Event.Event e) =>
        new(
            e.EventsId,
            e.TenantsId,
            e.Title,
            e.Slug,
            e.Description,
            e.ShortDescription,
            e.StoryDescription,
            e.Status,
            e.Category,
            e.StartDate,
            e.EndDate,
            e.ImagePath,
            e.HeroBackdropImageId,
            e.PosterImageId,
            e.IsFeatured,
            e.LayoutMode,
            e.EventType,
            e.VenuesId,
            e.TotalCapacity,
            e.FeesIncluded,
            e.AchEnabled,
            e.VenueCombinedTaxRate,
            e.IsVerifiedOrganizer,
            e.UrgencyBadgeText,
            e.PerformersJson,
            e.SponsorsJson,
            e.ExtraInfoJson);
}

public sealed record EventSummaryDto(
    string EventsId,
    string TenantsId,
    string Title,
    string Slug,
    string Description,
    string ShortDescription,
    string Status,
    string Category,
    long StartDate,
    long EndDate,
    string ImagePath,
    string HeroBackdropImageId,
    string PosterImageId,
    bool IsFeatured,
    string LayoutMode,
    string EventType,
    string VenuesId,
    int TotalCapacity,
    bool IsVerifiedOrganizer,
    string UrgencyBadgeText);

public sealed record EventDetailDto(
    string EventsId,
    string TenantsId,
    string Title,
    string Slug,
    string Description,
    string ShortDescription,
    string StoryDescription,
    string Status,
    string Category,
    long StartDate,
    long EndDate,
    string ImagePath,
    string HeroBackdropImageId,
    string PosterImageId,
    bool IsFeatured,
    string LayoutMode,
    string EventType,
    string VenuesId,
    int TotalCapacity,
    bool FeesIncluded,
    bool AchEnabled,
    double VenueCombinedTaxRate,
    bool IsVerifiedOrganizer,
    string UrgencyBadgeText,
    string PerformersJson,
    string SponsorsJson,
    string ExtraInfoJson);

public sealed record TicketTypeDto(
    string EventTicketTypesId,
    string Label,
    int PriceCents,
    int SellingPriceCents,
    int Capacity,
    int SoldCount,
    int MaxQuantity,
    string Description,
    int ServiceFeeCents,
    int TaxCents,
    int TotalCents);

public sealed record TableDto(
    string TablesId,
    string EventTablesId,
    string Label,
    int CapacityOverride,
    int PriceCents,
    string Status,
    double PosX,
    double PosY,
    double Width,
    double Height,
    string ShapeOverride,
    string ColorOverride);

public sealed record ScheduleItemDto(
    string ScheduleItemsId,
    string EventsId,
    string Title,
    string TypeCategory,
    long StartTime,
    long EndTime);
