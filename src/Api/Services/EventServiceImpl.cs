using System.Text;
using Grpc.Core;
using Npgsql;
using NpgsqlTypes;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Event;

namespace TicketSpan.Api.Services;

public sealed partial class EventServiceImpl : EventService.EventServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public EventServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<CreateEventResponse> CreateEvent(CreateEventRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        RequireNotEventScoped();
        if (tenantContext.UsersId is null || tenantContext.TenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authenticated tenant user required"));
        }
        
        
        if (request.EndDate <= request.StartDate)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "End date must be after start date"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_event(@t, @title, @slug, @desc, @status, @cat, @start, @end, @img, @feat, @layout, "
            + "NULL, NULL, NULL, @venue, @creator, @sched, @etype)", connection);
        cmd.Parameters.AddWithValue("t", tenantContext.TenantsId!.Value);
        cmd.Parameters.AddWithValue("title", request.Title);
        cmd.Parameters.AddWithValue("slug", request.Slug);
        cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", string.IsNullOrEmpty(request.Status) ? "Draft" : request.Status);
        cmd.Parameters.AddWithValue("cat", (object?)GetNormalizedCategory(request.Category) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("start", DateTimeOffset.FromUnixTimeSeconds(request.StartDate).UtcDateTime);
        cmd.Parameters.AddWithValue("end", DateTimeOffset.FromUnixTimeSeconds(request.EndDate).UtcDateTime);
        cmd.Parameters.AddWithValue("img", (object?)NullIfEmpty(request.ImagePath) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("feat", request.IsFeatured);
        cmd.Parameters.AddWithValue("layout", string.IsNullOrEmpty(request.LayoutMode) ? "Grid" : request.LayoutMode);
        cmd.Parameters.AddWithValue("etype", string.IsNullOrEmpty(request.EventType) ? "Open" : request.EventType);
        cmd.Parameters.AddWithValue("venue", Guid.Parse(request.VenuesId));
        cmd.Parameters.AddWithValue("creator", tenantContext.UsersId!.Value);
        cmd.Parameters.AddWithValue("sched", request.ScheduledPublishAt == 0
            ? DBNull.Value
            : DateTimeOffset.FromUnixTimeSeconds(request.ScheduledPublishAt).UtcDateTime);

        try
        {
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            return new CreateEventResponse { EventsId = id.ToString() };
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }
    }

    public override async Task<AckResponse> ChangeEventStatus(ChangeEventStatusRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_change_event_status(@id, @status)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("status", request.Status);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }
        return new AckResponse { Success = true, Message = "Status updated", Code = 0 };
    }

    public override async Task<ListEventsResponse> SearchEvents(SearchEventsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListEventsResponse { Meta = new PageMeta() };
        if (tenantContext.TenantsId is not { } tenantsId)
        {
            return response;
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT events_id, title, slug, status FROM vw_events WHERE events_id IN (SELECT events_id FROM sp_search_events(@q)) AND tenants_id = @tenant", connection);
        cmd.Parameters.AddWithValue("q", request.Query);
        cmd.Parameters.AddWithValue("tenant", tenantsId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Events.Add(new Event
            {
                EventsId = reader.GetGuid(0).ToString(),
                Title = reader.GetString(1),
                Slug = reader.GetString(2),
                Status = reader.GetString(3)
            });
        }
        response.Meta.Total = response.Events.Count;
        return response;
    }

    public override async Task<EventStats> GetEventStats(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.Value);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await RequireEventAccessAsync(connection, eventId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT total, paid, checked_in, revenue FROM sp_get_booking_stats(NULL, @ev)", connection);
        cmd.Parameters.AddWithValue("ev", eventId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new EventStats { EventsId = request.Value };
        }
        return new EventStats
        {
            EventsId = request.Value,
            TotalBookings = reader.GetInt32(0),
            TicketsSold = reader.GetInt32(1),
            CheckedIn = reader.GetInt32(2),
            RevenueCents = tenantContext.IsEventScoped ? 0 : reader.GetInt64(3)
        };
    }

    public override async Task<AckResponse> UpdateEvent(UpdateEventRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        
        
        if (request.StartDate != 0 && request.EndDate != 0 && request.EndDate <= request.StartDate)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "End date must be after start date"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_event(@id, @title, NULL, @desc, @cat, @start, @end, @img, @feat, NULL, NULL, NULL, NULL, @venue, NULL, @etype, @meta)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("title", (object?)NullIfEmpty(request.Title) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cat", (object?)GetNormalizedCategory(request.Category) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("start", request.StartDate == 0 ? DBNull.Value : DateTimeOffset.FromUnixTimeSeconds(request.StartDate).UtcDateTime);
        cmd.Parameters.AddWithValue("end", request.EndDate == 0 ? DBNull.Value : DateTimeOffset.FromUnixTimeSeconds(request.EndDate).UtcDateTime);
        cmd.Parameters.AddWithValue("img", (object?)NullIfEmpty(request.ImagePath) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("feat", request.IsFeatured);
        cmd.Parameters.AddWithValue("venue", string.IsNullOrEmpty(request.VenuesId) ? DBNull.Value : Guid.Parse(request.VenuesId));
        cmd.Parameters.AddWithValue("etype", string.IsNullOrEmpty(request.EventType) ? DBNull.Value : request.EventType);
        cmd.Parameters.Add(new NpgsqlParameter("meta", NpgsqlDbType.Jsonb)
        {
            Value = string.IsNullOrEmpty(request.ExtraInfoJson) ? DBNull.Value : request.ExtraInfoJson
        });
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }
        return new AckResponse { Success = true, Message = "Event updated" };
    }

    public override async Task<AckResponse> SetEventFeesIncluded(SetEventFeesIncludedRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_event_fees_included(@id, @inc)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("inc", request.FeesIncluded);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Fee display updated" };
    }

    public override async Task<AckResponse> SetEventAch(SetEventAchRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_event_ach(@id, @en)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("en", request.AchEnabled);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }
        return new AckResponse { Success = true, Message = request.AchEnabled ? "ACH enabled" : "ACH disabled" };
    }

    public override async Task<AckResponse> DeleteEvent(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        RequireNotEventScoped();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_delete_event(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }
        return new AckResponse { Success = true, Message = "Event deleted" };
    }

    public override async Task<Event> GetEvent(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(EventSelect + " WHERE events_id = @id" + EventScopeFilter, connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Event not found"));
        }
        return MapEvent(reader);
    }

    public override async Task<Event> GetEventBySlug(GetEventBySlugRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var isPublicViewer = tenantContext.UsersId is null || tenantContext.Role == Lookups.UserRoles.PublicViewer;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        var tenantFilter = tenantContext.TenantsId is null
            ? string.Empty
            : " AND tenants_id = @tenant";
        await using var cmd = new NpgsqlCommand(
            EventSelect + " WHERE slug = @slug"
            + tenantFilter + EventScopeFilter, connection);
        cmd.Parameters.AddWithValue("slug", request.Slug);
        if (tenantContext.TenantsId is { } tenantsId)
        {
            cmd.Parameters.AddWithValue("tenant", tenantsId);
        }
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Event not found"));
        }
        return MapEvent(reader);
    }

    public override async Task<ListEventsResponse> ListEvents(ListEventsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var page = request.Page ?? new PageRequest();
        var response = new ListEventsResponse { Meta = new PageMeta { Offset = page.Offset, Limit = page.Limit } };
        if (tenantContext.TenantsId is not { } tenantsId)
        {
            return response;
        }
        var isPublicViewer = tenantContext.UsersId is null || tenantContext.Role == Lookups.UserRoles.PublicViewer;
        var effectiveStatus = isPublicViewer ? "Published" : (request.Status ?? string.Empty);
        var hasStatus = effectiveStatus.Length > 0;

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        
        var sqlBuilder = new StringBuilder(EventSelect);
        sqlBuilder.Append(" WHERE tenants_id = @tenant");
        if (hasStatus)
        {
            sqlBuilder.Append(" AND status = @status");
        }
        sqlBuilder.Append(EventScopeFilter);
        sqlBuilder.Append(" ORDER BY start_date DESC LIMIT @lim OFFSET @off");

        await using var cmd = new NpgsqlCommand(sqlBuilder.ToString(), connection);
        cmd.Parameters.AddWithValue("tenant", tenantsId);
        if (hasStatus)
        {
            cmd.Parameters.AddWithValue("status", effectiveStatus);
        }
        cmd.Parameters.AddWithValue("lim", page.Limit <= 0 ? 25 : page.Limit);
        cmd.Parameters.AddWithValue("off", page.Offset);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Events.Add(MapEvent(reader));
        }
        response.Meta.Total = response.Events.Count;
        return response;
    }

    public override async Task<ListScheduleItemsResponse> ListScheduleItems(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListScheduleItemsResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await RequireEventAccessAsync(connection, Guid.Parse(request.Value), ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT schedule_items_id, events_id, title, type_category, start_time, end_time "
            + "FROM vw_schedule_items WHERE events_id = @ev ORDER BY start_time", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Items.Add(MapScheduleItem(reader));
        }
        return response;
    }

    public override async Task<UuidValue> CreateScheduleItem(CreateScheduleItemRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        if (tenantContext.TenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authenticated tenant user required"));
        }
        if (request.EndTime <= request.StartTime)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "End time must be after start time"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_schedule_item(@ev, @t, @title, @cat, @start, @end)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("t", tenantContext.TenantsId!.Value);
        cmd.Parameters.AddWithValue("title", request.Title);
        cmd.Parameters.AddWithValue("cat", request.TypeCategory);
        cmd.Parameters.AddWithValue("start", DateTimeOffset.FromUnixTimeSeconds(request.StartTime).UtcDateTime);
        cmd.Parameters.AddWithValue("end", DateTimeOffset.FromUnixTimeSeconds(request.EndTime).UtcDateTime);
        try
        {
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            return new UuidValue { Value = id.ToString() };
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }
    }

    public override async Task<AckResponse> UpdateScheduleItem(UpdateScheduleItemRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        if (request.StartTime != 0 && request.EndTime != 0 && request.EndTime <= request.StartTime)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "End time must be after start time"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_schedule_item(@id, @title, @cat, @start, @end)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.ScheduleItemsId));
        cmd.Parameters.AddWithValue("title", (object?)NullIfEmpty(request.Title) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cat", (object?)NullIfEmpty(request.TypeCategory) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("start", request.StartTime == 0 ? DBNull.Value : DateTimeOffset.FromUnixTimeSeconds(request.StartTime).UtcDateTime);
        cmd.Parameters.AddWithValue("end", request.EndTime == 0 ? DBNull.Value : DateTimeOffset.FromUnixTimeSeconds(request.EndTime).UtcDateTime);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }
        return new AckResponse { Success = true, Message = "Schedule item updated" };
    }

    public override async Task<AckResponse> DeleteScheduleItem(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_delete_schedule_item(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Schedule item deleted" };
    }

    public override async Task<ListEventImagesResponse> ListEventImages(ListEventImagesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListEventImagesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await RequireEventAccessAsync(connection, Guid.Parse(request.EventsId), ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT images_id, storage_key, type, is_primary, sort_order "
            + "FROM sp_list_event_images(@ev, @type)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("type", (object?)NullIfEmpty(request.Type) ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Images.Add(MapEventImage(reader));
        }
        return response;
    }

    public override async Task<EventImage> AddEventImage(AddEventImageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var type = NullIfEmpty(request.Type) ?? "event_image";
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT i.storage_key, l.image_type, l.is_primary, l.sort_order "
            + "FROM sp_link_event_image(@ev, @img, @type) l "
            + "JOIN vw_images i ON i.images_id = @img", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("img", Guid.Parse(request.ImagesId));
        cmd.Parameters.AddWithValue("type", type);
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to link image"));
            }
            return new EventImage
            {
                ImagesId = request.ImagesId,
                StorageKey = reader.GetString(0),
                Type = reader.GetString(1),
                IsPrimary = reader.GetBoolean(2),
                SortOrder = reader.GetInt32(3)
            };
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }
    }

    public override async Task<AckResponse> RemoveEventImage(RemoveEventImageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_remove_event_image(@ev, @img)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("img", Guid.Parse(request.ImagesId));
        var ok = (bool)(await cmd.ExecuteScalarAsync(ct))!;
        return new AckResponse { Success = ok, Message = ok ? "Image removed" : "Image not found" };
    }

    public override async Task<AckResponse> SetPrimaryEventImage(RemoveEventImageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_event_primary_image(@ev, @img)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("img", Guid.Parse(request.ImagesId));
        var ok = (bool)(await cmd.ExecuteScalarAsync(ct))!;
        return new AckResponse { Success = ok, Message = ok ? "Primary image set" : "Image not found" };
    }

    public override async Task<AckResponse> ReorderEventImages(ReorderEventImagesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var ids = request.ImagesId.Select(Guid.Parse).ToArray();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_reorder_event_images(@ev, @type, @ids)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("type", NullIfEmpty(request.Type) ?? "event_image");
        cmd.Parameters.AddWithValue("ids", ids);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Images reordered" };
    }

    public override async Task<MediaSettings> GetMediaSettings(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT key, value FROM vw_app_settings WHERE key IN ('event_image_aspect_ratio', 'event_thumbnail_aspect_ratio')",
            connection);
        var settings = new MediaSettings { EventImageAspectRatio = "16:9", EventThumbnailAspectRatio = "4:3" };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (reader.GetString(0) == "event_image_aspect_ratio")
            {
                settings.EventImageAspectRatio = reader.GetString(1);
            }
            else
            {
                settings.EventThumbnailAspectRatio = reader.GetString(1);
            }
        }
        return settings;
    }

    private string EventScopeFilter =>
        tenantContext.IsEventScoped ? " AND app.can_access_event(events_id)" : string.Empty;

    private void RequireTenant()
    {
        if (tenantContext.TenantsId is null && !tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
    }

    private void RequireNotEventScoped()
    {
        if (tenantContext.IsEventScoped)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Event managers cannot create or delete events"));
        }
    }

    private Task RequireEventAccessAsync(NpgsqlConnection connection, Guid eventId, CancellationToken ct) =>
        EventAccess.RequireAsync(connection, tenantContext, eventId, ct);
}
