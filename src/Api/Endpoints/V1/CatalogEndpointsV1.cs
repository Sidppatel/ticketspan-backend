using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TicketSpan.Api.Endpoints.Common;
using TicketSpan.Api.Services;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Catalog;

namespace TicketSpan.Api.Endpoints.V1;

public static class CatalogEndpointsV1
{
    public static RouteGroupBuilder MapCatalogApiV1(this RouteGroupBuilder group)
    {
        var catalog = group.MapGroup("/catalog").WithTags("Catalog");

        catalog.MapGet("/venues", async (
            int? offset,
            int? limit,
            string? search,
            VenueServiceImpl venueService,
            CancellationToken ct) =>
        {
            var off = offset is >= 0 ? offset.Value : 0;
            var lim = limit is > 0 and <= 100 ? limit.Value : 50;

            try
            {
                var res = await venueService.ListVenues(new PageRequest
                {
                    Offset = off,
                    Limit = lim,
                    Search = search ?? string.Empty
                }, new UnaryServerCallContext(ct));

                var items = res.Venues.Select(v => new VenueDto(
                    v.VenuesId,
                    v.Name,
                    v.Description,
                    v.ImagePath,
                    v.Phone,
                    v.Email,
                    v.Website,
                    v.Line1,
                    v.Line2,
                    v.City,
                    v.State,
                    v.Zip,
                    v.CombinedTaxRate)).ToList();

                var total = res.Meta?.Total ?? items.Count;
                return Results.Ok(new PagedEnvelope<VenueDto>(true, items, total, off / lim + 1, lim));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        catalog.MapGet("/venues/{id}", async (
            string id,
            VenueServiceImpl venueService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out _))
            {
                return Results.BadRequest(new ApiEnvelope<VenueDto>(false, null, "Invalid venue ID"));
            }

            try
            {
                var v = await venueService.GetVenue(new UuidValue { Value = id }, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<VenueDto>(true, new VenueDto(
                    v.VenuesId,
                    v.Name,
                    v.Description,
                    v.ImagePath,
                    v.Phone,
                    v.Email,
                    v.Website,
                    v.Line1,
                    v.Line2,
                    v.City,
                    v.State,
                    v.Zip,
                    v.CombinedTaxRate)));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                return Results.NotFound(new ApiEnvelope<VenueDto>(false, null, "Venue not found"));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        catalog.MapGet("/performers", async (
            int? offset,
            int? limit,
            string? search,
            PerformerServiceImpl performerService,
            CancellationToken ct) =>
        {
            var off = offset is >= 0 ? offset.Value : 0;
            var lim = limit is > 0 and <= 100 ? limit.Value : 50;

            try
            {
                var res = await performerService.ListPerformers(new PageRequest
                {
                    Offset = off,
                    Limit = lim,
                    Search = search ?? string.Empty
                }, new UnaryServerCallContext(ct));

                var items = res.Performers.Select(pItem => new PerformerDto(
                    pItem.PerformersId,
                    pItem.Name,
                    pItem.Slug,
                    pItem.PrimaryImagePath,
                    pItem.MetaJson,
                    pItem.IsActive)).ToList();

                var total = res.Meta?.Total ?? items.Count;
                return Results.Ok(new PagedEnvelope<PerformerDto>(true, items, total, off / lim + 1, lim));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        catalog.MapGet("/performers/{idOrSlug}", async (
            string idOrSlug,
            PerformerServiceImpl performerService,
            CancellationToken ct) =>
        {
            try
            {
                var p = await performerService.GetPerformerBySlug(new GetBySlugRequest { Slug = idOrSlug }, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<PublicPerformerDto>(true, new PublicPerformerDto(
                    p.PerformersId,
                    p.Name,
                    p.Slug,
                    p.PrimaryImagePath,
                    p.MetaJson,
                    p.Events.Select(e => new PublicLinkedEventDto(e.EventsId, e.Title, e.Slug, e.StartDate, e.PrimaryImagePath, e.Category)).ToList())));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                return Results.NotFound(new ApiEnvelope<PublicPerformerDto>(false, null, "Performer not found"));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        catalog.MapGet("/sponsors", async (
            int? offset,
            int? limit,
            string? search,
            SponsorServiceImpl sponsorService,
            CancellationToken ct) =>
        {
            var off = offset is >= 0 ? offset.Value : 0;
            var lim = limit is > 0 and <= 100 ? limit.Value : 50;

            try
            {
                var res = await sponsorService.ListSponsors(new PageRequest
                {
                    Offset = off,
                    Limit = lim,
                    Search = search ?? string.Empty
                }, new UnaryServerCallContext(ct));

                var items = res.Sponsors.Select(s => new SponsorDto(
                    s.SponsorsId,
                    s.Name,
                    s.Slug,
                    s.PrimaryImagePath,
                    s.MetaJson,
                    s.IsActive)).ToList();

                var total = res.Meta?.Total ?? items.Count;
                return Results.Ok(new PagedEnvelope<SponsorDto>(true, items, total, off / lim + 1, lim));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        catalog.MapGet("/sponsors/{idOrSlug}", async (
            string idOrSlug,
            SponsorServiceImpl sponsorService,
            CancellationToken ct) =>
        {
            try
            {
                var s = await sponsorService.GetSponsorBySlug(new GetBySlugRequest { Slug = idOrSlug }, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<PublicSponsorDto>(true, new PublicSponsorDto(
                    s.SponsorsId,
                    s.Name,
                    s.Slug,
                    s.PrimaryImagePath,
                    s.MetaJson,
                    s.Events.Select(e => new PublicLinkedEventDto(e.EventsId, e.Title, e.Slug, e.StartDate, e.PrimaryImagePath, e.Category)).ToList())));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                return Results.NotFound(new ApiEnvelope<PublicSponsorDto>(false, null, "Sponsor not found"));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        return group;
    }
}

public sealed record VenueDto(
    string VenuesId,
    string Name,
    string Description,
    string ImagePath,
    string Phone,
    string Email,
    string Website,
    string Line1,
    string Line2,
    string City,
    string State,
    string Zip,
    double CombinedTaxRate);

public sealed record PerformerDto(
    string PerformersId,
    string Name,
    string Slug,
    string PrimaryImagePath,
    string MetaJson,
    bool IsActive);

public sealed record SponsorDto(
    string SponsorsId,
    string Name,
    string Slug,
    string PrimaryImagePath,
    string MetaJson,
    bool IsActive);

public sealed record PublicPerformerDto(
    string PerformersId,
    string Name,
    string Slug,
    string PrimaryImagePath,
    string MetaJson,
    IReadOnlyList<PublicLinkedEventDto> Events);

public sealed record PublicSponsorDto(
    string SponsorsId,
    string Name,
    string Slug,
    string PrimaryImagePath,
    string MetaJson,
    IReadOnlyList<PublicLinkedEventDto> Events);

public sealed record PublicLinkedEventDto(
    string EventsId,
    string Title,
    string Slug,
    long StartDate,
    string PrimaryImagePath,
    string Category);
