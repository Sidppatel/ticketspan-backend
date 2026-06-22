using Grpc.Core;
using Npgsql;
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
        if (tenantContext.UsersId is null || tenantContext.TenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authenticated tenant user required"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_event(@t, @title, @slug, @desc, @status, @cat, @start, @end, @img, @feat, @layout, "
            + "@maxcap, NULL, NULL, NULL, @rows, @cols, @venue, @creator, @sched)", connection);
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
        cmd.Parameters.AddWithValue("maxcap", request.MaxCapacity == 0 ? DBNull.Value : request.MaxCapacity);
        cmd.Parameters.AddWithValue("rows", request.GridRows == 0 ? DBNull.Value : request.GridRows);
        cmd.Parameters.AddWithValue("cols", request.GridCols == 0 ? DBNull.Value : request.GridCols);
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
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Status updated", Code = 0 };
    }

    public override async Task<ListEventsResponse> SearchEvents(SearchEventsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListEventsResponse { Meta = new PageMeta() };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT e.events_id, e.title, e.slug, e.status FROM sp_search_events(@q) s JOIN events e ON e.events_id = s.events_id", connection);
        cmd.Parameters.AddWithValue("q", request.Query);
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
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT total, paid, checked_in, revenue FROM sp_get_purchase_stats(NULL, @ev)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new EventStats { EventsId = request.Value };
        }
        return new EventStats
        {
            EventsId = request.Value,
            TotalPurchases = reader.GetInt32(0),
            TicketsSold = reader.GetInt32(1),
            CheckedIn = reader.GetInt32(2),
            RevenueCents = reader.GetInt64(3)
        };
    }

    public override async Task<AckResponse> UpdateEvent(UpdateEventRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_event(@id, @title, NULL, @desc, @cat, @start, @end, @img, @feat, NULL, @maxcap, NULL, NULL, NULL, NULL, NULL, @venue, NULL)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("title", (object?)NullIfEmpty(request.Title) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cat", (object?)NullIfEmpty(request.Category) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("start", request.StartDate == 0 ? DBNull.Value : DateTimeOffset.FromUnixTimeSeconds(request.StartDate).UtcDateTime);
        cmd.Parameters.AddWithValue("end", request.EndDate == 0 ? DBNull.Value : DateTimeOffset.FromUnixTimeSeconds(request.EndDate).UtcDateTime);
        cmd.Parameters.AddWithValue("img", (object?)NullIfEmpty(request.ImagePath) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("feat", request.IsFeatured);
        cmd.Parameters.AddWithValue("maxcap", request.MaxCapacity == 0 ? DBNull.Value : request.MaxCapacity);
        cmd.Parameters.AddWithValue("venue", string.IsNullOrEmpty(request.VenuesId) ? DBNull.Value : Guid.Parse(request.VenuesId));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Event updated" };
    }

    public override async Task<AckResponse> DeleteEvent(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_delete_event(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Event deleted" };
    }

    public override async Task<Event> GetEvent(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(EventSelect + " WHERE events_id = @id", connection);
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
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(EventSelect + " WHERE slug = @slug", connection);
        cmd.Parameters.AddWithValue("slug", request.Slug);
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
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            EventSelect + " WHERE (@status = '' OR status = @status) ORDER BY start_date DESC LIMIT @lim OFFSET @off", connection);
        cmd.Parameters.AddWithValue("status", request.Status ?? string.Empty);
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

    private const string EventSelect =
        "SELECT events_id, title, slug, description, status, category, start_date, end_date, image_path, "
        + "is_featured, layout_mode, max_capacity, venues_id, performers::text, sponsors::text FROM vw_events";

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
        MaxCapacity = r.IsDBNull(11) ? 0 : r.GetInt32(11),
        VenuesId = r.IsDBNull(12) ? string.Empty : r.GetGuid(12).ToString(),
        PerformersJson = r.IsDBNull(13) ? "[]" : r.GetString(13),
        SponsorsJson = r.IsDBNull(14) ? "[]" : r.GetString(14)
    };

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private void RequireTenant()
    {
        if (tenantContext.TenantsId is null && !tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
    }
}
