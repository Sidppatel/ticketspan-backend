using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TicketSpan.Api.Endpoints.Common;
using TicketSpan.Api.Services;
using TicketSpan.Protos.Admin;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Event;
using TicketSpan.Protos.Reporting;

namespace TicketSpan.Api.Endpoints.V1;

public static class AdminEndpointsV1
{
    public static RouteGroupBuilder MapAdminApiV1(this RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin").WithTags("Admin");

        admin.MapGet("/dashboard", async (
            DashboardServiceImpl dashboardService,
            CancellationToken ct) =>
        {
            try
            {
                var res = await dashboardService.GetAdminDashboard(new Empty(), new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<AdminDashboardApiResponse>(true, new AdminDashboardApiResponse(
                    res.TotalEvents,
                    res.ActiveEvents,
                    res.TotalRevenueCents,
                    res.TotalAttendees)));
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

        admin.MapPost("/events", async (
            CreateAdminEventApiRequest request,
            EventServiceImpl eventService,
            CancellationToken ct) =>
        {
            try
            {
                var req = new CreateEventRequest
                {
                    Title = request.Title,
                    Slug = request.Slug,
                    Description = request.Description ?? string.Empty,
                    ShortDescription = request.ShortDescription ?? string.Empty,
                    StoryDescription = request.StoryDescription ?? string.Empty,
                    Status = request.Status ?? "Draft",
                    Category = request.Category ?? string.Empty,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    ImagePath = request.ImagePath ?? string.Empty,
                    HeroBackdropImageId = request.HeroBackdropImageId ?? string.Empty,
                    PosterImageId = request.PosterImageId ?? string.Empty,
                    IsFeatured = request.IsFeatured,
                    LayoutMode = request.LayoutMode ?? "Grid",
                    EventType = request.EventType ?? "Open",
                    VenuesId = request.VenuesId,
                    IsVerifiedOrganizer = request.IsVerifiedOrganizer,
                    UrgencyBadgeText = request.UrgencyBadgeText ?? string.Empty
                };

                var res = await eventService.CreateEvent(req, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<CreateAdminEventApiResponse>(true, new CreateAdminEventApiResponse(res.EventsId)));
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

        admin.MapPut("/events/{id}", async (
            string id,
            UpdateAdminEventApiRequest request,
            EventServiceImpl eventService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out _))
            {
                return Results.BadRequest(new AckEnvelope(false, "Invalid event ID", 400));
            }

            try
            {
                var req = new UpdateEventRequest
                {
                    EventsId = id,
                    Title = request.Title ?? string.Empty,
                    Description = request.Description ?? string.Empty,
                    ShortDescription = request.ShortDescription ?? string.Empty,
                    StoryDescription = request.StoryDescription ?? string.Empty,
                    Category = request.Category ?? string.Empty,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    ImagePath = request.ImagePath ?? string.Empty,
                    HeroBackdropImageId = request.HeroBackdropImageId ?? string.Empty,
                    PosterImageId = request.PosterImageId ?? string.Empty,
                    IsFeatured = request.IsFeatured,
                    VenuesId = request.VenuesId ?? string.Empty,
                    EventType = request.EventType ?? string.Empty,
                    IsVerifiedOrganizer = request.IsVerifiedOrganizer,
                    UrgencyBadgeText = request.UrgencyBadgeText ?? string.Empty
                };

                var res = await eventService.UpdateEvent(req, new UnaryServerCallContext(ct));
                return Results.Ok(new AckEnvelope(res.Success, res.Message, res.Code));
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

        admin.MapPost("/events/{id}/status", async (
            string id,
            ChangeEventStatusApiRequest request,
            EventServiceImpl eventService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out _))
            {
                return Results.BadRequest(new AckEnvelope(false, "Invalid event ID", 400));
            }

            try
            {
                var req = new ChangeEventStatusRequest
                {
                    EventsId = id,
                    Status = request.Status
                };

                var res = await eventService.ChangeEventStatus(req, new UnaryServerCallContext(ct));
                return Results.Ok(new AckEnvelope(res.Success, res.Message, res.Code));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
            {
                return Results.Unauthorized();
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status412PreconditionFailed);
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        admin.MapGet("/reports/summary", async (
            long? from,
            long? to,
            ReportingServiceImpl reportingService,
            CancellationToken ct) =>
        {
            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var req = new ReportRangeRequest
                {
                    FromEpochSeconds = from ?? (now - 30 * 86400),
                    ToEpochSeconds = to ?? now
                };

                var res = await reportingService.GetReportSummary(req, new UnaryServerCallContext(ct));
                return Results.Ok(new ApiEnvelope<ReportSummaryApiResponse>(true, new ReportSummaryApiResponse(
                    res.RevenueCents,
                    res.Orders,
                    res.TicketsSold,
                    res.AverageOrderCents,
                    res.Visits,
                    res.ConversionBps,
                    res.RefundedCents,
                    res.RefundedOrders,
                    res.NetRevenueCents,
                    res.ServiceFeeCents,
                    res.TaxCents)));
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

        admin.MapGet("/reports/timeseries", async (
            long? from,
            long? to,
            string? bucket,
            ReportingServiceImpl reportingService,
            CancellationToken ct) =>
        {
            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var req = new TimeseriesRequest
                {
                    FromEpochSeconds = from ?? (now - 30 * 86400),
                    ToEpochSeconds = to ?? now,
                    Bucket = string.IsNullOrEmpty(bucket) ? "day" : bucket
                };

                var res = await reportingService.GetRevenueTimeseries(req, new UnaryServerCallContext(ct));
                var items = res.Points.Select(b => new TimeseriesPointDto(
                    b.BucketStartEpochSeconds,
                    b.RevenueCents,
                    b.Orders,
                    b.TicketsSold)).ToList();

                return Results.Ok(new ApiEnvelope<IReadOnlyList<TimeseriesPointDto>>(true, items));
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

        admin.MapPost("/maintenance/prune", async (
            ITableCleanupService cleanupService,
            CancellationToken ct) =>
        {
            try
            {
                var summary = await cleanupService.RunFullCleanupAsync(ct);
                return Results.Ok(new ApiEnvelope<TableCleanupSummary>(true, summary));
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }).RequireAuthorization();

        return group;
    }
}

public sealed record AdminDashboardApiResponse(
    int TotalEvents,
    int ActiveEvents,
    long TotalRevenueCents,
    int TotalAttendees);

public sealed record CreateAdminEventApiRequest(
    string Title,
    string Slug,
    string? Description,
    string? ShortDescription,
    string? StoryDescription,
    string? Status,
    string? Category,
    long StartDate,
    long EndDate,
    string? ImagePath,
    string? HeroBackdropImageId,
    string? PosterImageId,
    bool IsFeatured,
    string? LayoutMode,
    string? EventType,
    string VenuesId,
    bool IsVerifiedOrganizer,
    string? UrgencyBadgeText);

public sealed record CreateAdminEventApiResponse(string EventsId);

public sealed record UpdateAdminEventApiRequest(
    string? Title,
    string? Description,
    string? ShortDescription,
    string? StoryDescription,
    string? Category,
    long StartDate,
    long EndDate,
    string? ImagePath,
    string? HeroBackdropImageId,
    string? PosterImageId,
    bool IsFeatured,
    string? VenuesId,
    string? EventType,
    bool IsVerifiedOrganizer,
    string? UrgencyBadgeText);

public sealed record ChangeEventStatusApiRequest(string Status);

public sealed record ReportSummaryApiResponse(
    long RevenueCents,
    int Orders,
    int TicketsSold,
    long AverageOrderCents,
    int Visits,
    int ConversionBps,
    long RefundedCents,
    int RefundedOrders,
    long NetRevenueCents,
    long ServiceFeeCents,
    long TaxCents);

public sealed record TimeseriesPointDto(
    long BucketStartEpochSeconds,
    long RevenueCents,
    int Orders,
    int TicketsSold);
