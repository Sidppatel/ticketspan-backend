using Grpc.Core;
using Npgsql;
using NpgsqlTypes;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Event;

namespace Svyne.Api.Services;

public sealed class EventServiceImpl : EventService.EventServiceBase
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
        // Guard the events_DateRange check constraint with a clean error instead of a
        // raw Postgres 23514 (the form may submit empty/equal/reversed dates).
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
        cmd.Parameters.AddWithValue("cat", request.Category ?? string.Empty);
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

        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new CreateEventResponse { EventsId = id.ToString() };
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
            "SELECT e.events_id, e.title, e.slug, e.status FROM sp_search_events(@q) s JOIN events e ON e.events_id = s.events_id WHERE e.tenants_id = @tenant", connection);
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
        // Same date-range guard as CreateEvent, but only when both dates are supplied
        // (0 = leave unchanged).
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
        cmd.Parameters.AddWithValue("cat", (object?)NullIfEmpty(request.Category) ?? DBNull.Value);
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
        await cmd.ExecuteNonQueryAsync(ct);
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
        var isPublicViewer = tenantContext.UsersId is null || tenantContext.Role == 0;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        var tenantFilter = tenantContext.TenantsId is null
            ? string.Empty
            : " AND events_id IN (SELECT events_id FROM events WHERE tenants_id = @tenant)";
        await using var cmd = new NpgsqlCommand(
            EventSelect + " WHERE slug = @slug"
            + (isPublicViewer ? " AND status = 'Published'" : string.Empty)
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
        var isPublicViewer = tenantContext.UsersId is null || tenantContext.Role == 0;
        var effectiveStatus = isPublicViewer ? "Published" : (request.Status ?? string.Empty);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            EventSelect
            + " WHERE events_id IN (SELECT events_id FROM events WHERE tenants_id = @tenant)"
            + " AND (@status = '' OR status = @status)"
            + EventScopeFilter
            + " ORDER BY start_date DESC LIMIT @lim OFFSET @off", connection);
        cmd.Parameters.AddWithValue("tenant", tenantsId);
        cmd.Parameters.AddWithValue("status", effectiveStatus);
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
            + "JOIN images i ON i.images_id = @img", connection);
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
            "SELECT key, value FROM app_settings WHERE key IN ('event_image_aspect_ratio', 'event_thumbnail_aspect_ratio')",
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

    private static EventImage MapEventImage(NpgsqlDataReader r) => new()
    {
        ImagesId = r.GetGuid(0).ToString(),
        StorageKey = r.GetString(1),
        Type = r.GetString(2),
        IsPrimary = r.GetBoolean(3),
        SortOrder = r.GetInt32(4)
    };

    private static ScheduleItem MapScheduleItem(NpgsqlDataReader r) => new()
    {
        ScheduleItemsId = r.GetGuid(0).ToString(),
        EventsId = r.GetGuid(1).ToString(),
        Title = r.GetString(2),
        TypeCategory = r.GetString(3),
        StartTime = new DateTimeOffset(r.GetDateTime(4), TimeSpan.Zero).ToUnixTimeSeconds(),
        EndTime = new DateTimeOffset(r.GetDateTime(5), TimeSpan.Zero).ToUnixTimeSeconds()
    };

    private static RpcException MapPostgres(PostgresException ex) => ex.SqlState switch
    {
        "P0001" => new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText)),
        "P0002" => new RpcException(new Status(StatusCode.NotFound, ex.MessageText)),
        "22023" => new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText)),
        "23P01" => new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText)),
        "23514" => new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText)),
        _ => new RpcException(new Status(StatusCode.Internal, ex.MessageText))
    };

    private const string EventSelect =
        "SELECT events_id, title, slug, description, status, category, start_date, end_date, image_path, "
        + "is_featured, layout_mode, total_capacity, venues_id, performers::text, sponsors::text, fees_included, event_type, primary_image_id, extra_info::text FROM vw_events";

    private static Event MapEvent(NpgsqlDataReader r) => new()
    {
        EventsId = r.GetGuid(0).ToString(),
        Title = r.GetString(1),
        Slug = r.GetString(2),
        Description = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        Status = r.IsDBNull(4) ? string.Empty : r.GetString(4),
        Category = r.IsDBNull(5) ? string.Empty : r.GetString(5),
        StartDate = new DateTimeOffset(r.GetDateTime(6), TimeSpan.Zero).ToUnixTimeSeconds(),
        EndDate = new DateTimeOffset(r.GetDateTime(7), TimeSpan.Zero).ToUnixTimeSeconds(),
        ImagePath = r.IsDBNull(8) ? string.Empty : r.GetString(8),
        IsFeatured = !r.IsDBNull(9) && r.GetBoolean(9),
        LayoutMode = r.IsDBNull(10) ? string.Empty : r.GetString(10),
        TotalCapacity = r.IsDBNull(11) ? 0 : r.GetInt32(11),
        VenuesId = r.IsDBNull(12) ? string.Empty : r.GetGuid(12).ToString(),
        PerformersJson = r.IsDBNull(13) ? "[]" : r.GetString(13),
        SponsorsJson = r.IsDBNull(14) ? "[]" : r.GetString(14),
        FeesIncluded = !r.IsDBNull(15) && r.GetBoolean(15),
        EventType = r.IsDBNull(16) ? string.Empty : r.GetString(16),
        PrimaryImageId = r.IsDBNull(17) ? string.Empty : r.GetGuid(17).ToString(),
        ExtraInfoJson = r.IsDBNull(18) ? "[]" : r.GetString(18)
    };

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

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
