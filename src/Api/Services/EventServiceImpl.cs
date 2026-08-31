using System.Text;
using Grpc.Core;
using Npgsql;
using NpgsqlTypes;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Event;

namespace TicketSpan.Api.Services;

public sealed partial class EventServiceImpl : EventService.EventServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly IEmailService emailService;
    private readonly EmailTemplateRenderer templates;
    private readonly AppSettingsProvider settings;
    private readonly ILogger<EventServiceImpl> logger;
    private readonly IConfiguration configuration;

    public EventServiceImpl(
        Db db,
        TenantContext tenantContext,
        IEmailService emailService,
        EmailTemplateRenderer templates,
        AppSettingsProvider settings,
        ILogger<EventServiceImpl> logger,
        IConfiguration configuration)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.emailService = emailService;
        this.templates = templates;
        this.settings = settings;
        this.logger = logger;
        this.configuration = configuration;
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
            + "NULL, NULL, NULL, @venue, @creator, @sched, @etype, @short_desc, @story_desc, @hero_img, @poster_img, @verified, @urgency, @tax_exempt)", connection);
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
        cmd.Parameters.AddWithValue("short_desc", (object?)NullIfEmpty(request.ShortDescription) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("story_desc", (object?)NullIfEmpty(request.StoryDescription) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hero_img", string.IsNullOrEmpty(request.HeroBackdropImageId) ? DBNull.Value : Guid.Parse(request.HeroBackdropImageId));
        cmd.Parameters.AddWithValue("poster_img", string.IsNullOrEmpty(request.PosterImageId) ? DBNull.Value : Guid.Parse(request.PosterImageId));
        cmd.Parameters.AddWithValue("verified", request.IsVerifiedOrganizer);
        cmd.Parameters.AddWithValue("urgency", (object?)NullIfEmpty(request.UrgencyBadgeText) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tax_exempt", request.TaxExempt ? true : (object)DBNull.Value);

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
            "SELECT events_id, title, slug, status FROM sp_search_events(@q, @tenant)", connection);
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
            "SELECT sp_update_event(@id, @title, NULL, @desc, @cat, @start, @end, @img, @feat, NULL, NULL, NULL, NULL, @venue, NULL, @etype, @meta, @short_desc, @story_desc, @hero_img, @poster_img, @verified, @urgency, @tax_exempt)", connection);
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
        cmd.Parameters.AddWithValue("short_desc", (object?)NullIfEmpty(request.ShortDescription) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("story_desc", (object?)NullIfEmpty(request.StoryDescription) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hero_img", string.IsNullOrEmpty(request.HeroBackdropImageId) ? DBNull.Value : Guid.Parse(request.HeroBackdropImageId));
        cmd.Parameters.AddWithValue("poster_img", string.IsNullOrEmpty(request.PosterImageId) ? DBNull.Value : Guid.Parse(request.PosterImageId));
        cmd.Parameters.AddWithValue("verified", request.IsVerifiedOrganizer);
        cmd.Parameters.AddWithValue("urgency", (object?)NullIfEmpty(request.UrgencyBadgeText) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tax_exempt", request.TaxExempt);
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

    public override async Task<AckResponse> SetEventTaxExempt(SetEventTaxExemptRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var eventId = Guid.Parse(request.EventsId);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await RequireEventAccessAsync(connection, eventId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_event_tax_exempt(@id, @ex)", connection);
        cmd.Parameters.AddWithValue("id", eventId);
        cmd.Parameters.AddWithValue("ex", request.TaxExempt);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }
        return new AckResponse
        {
            Success = true,
            Message = request.TaxExempt ? "Sales tax disabled (event marked tax-exempt)" : "Sales tax enabled for event"
        };
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
        var limit = page.Limit <= 0 ? 15 : page.Limit;
        var offset = page.Offset < 0 ? 0 : page.Offset;
        var response = new ListEventsResponse { Meta = new PageMeta { Offset = offset, Limit = limit } };
        var isPublicViewer = tenantContext.UsersId is null || tenantContext.Role == Lookups.UserRoles.PublicViewer;
        var effectiveStatus = isPublicViewer ? "Published" : (request.Status ?? string.Empty);
        var hasStatus = effectiveStatus.Length > 0;

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        var whereClauses = new List<string>();
        var parameters = new List<NpgsqlParameter>();

        if (tenantContext.TenantsId is { } tenantsId)
        {
            whereClauses.Add("tenants_id = @tenant");
            parameters.Add(new NpgsqlParameter("tenant", tenantsId));
            if (hasStatus)
            {
                whereClauses.Add("status = @status");
                parameters.Add(new NpgsqlParameter("status", effectiveStatus));
            }
            if (!string.IsNullOrEmpty(EventScopeFilter))
            {
                whereClauses.Add(EventScopeFilter.TrimStart().StartsWith("AND ", StringComparison.OrdinalIgnoreCase) 
                    ? EventScopeFilter.TrimStart()[4..] 
                    : EventScopeFilter);
            }
        }
        else if (isPublicViewer)
        {
            whereClauses.Add("status = 'Published'");
        }
        else
        {
            return response;
        }

        if (request.UpcomingOnly || isPublicViewer)
        {
            var cutoff = DateTime.UtcNow.AddDays(-1);
            whereClauses.Add("(start_date >= @upcomingCutoff OR (end_date IS NOT NULL AND end_date >= @upcomingCutoff))");
            parameters.Add(new NpgsqlParameter("upcomingCutoff", cutoff));
        }

        if (!string.IsNullOrWhiteSpace(request.Category) && !string.Equals(request.Category, "All", StringComparison.OrdinalIgnoreCase))
        {
            whereClauses.Add("LOWER(category) = LOWER(@category)");
            parameters.Add(new NpgsqlParameter("category", request.Category.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(request.TenantSlug) && !string.Equals(request.TenantSlug, "all", StringComparison.OrdinalIgnoreCase))
        {
            whereClauses.Add("LOWER(tenant_slug) = LOWER(@tenantSlug)");
            parameters.Add(new NpgsqlParameter("tenantSlug", request.TenantSlug.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(request.DateFilter) && !string.Equals(request.DateFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            var nowUtc = DateTime.UtcNow;
            var startOfToday = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
            var endOfToday = startOfToday.AddDays(1).AddTicks(-1);

            switch (request.DateFilter.ToLowerInvariant())
            {
                case "today":
                    whereClauses.Add("start_date >= @startOfToday AND start_date <= @endOfToday");
                    parameters.Add(new NpgsqlParameter("startOfToday", startOfToday));
                    parameters.Add(new NpgsqlParameter("endOfToday", endOfToday));
                    break;
                case "weekend":
                    var day = (int)nowUtc.DayOfWeek;
                    var daysUntilFriday = (5 - day + 7) % 7;
                    var friday = startOfToday.AddDays(daysUntilFriday);
                    var sunday = friday.AddDays(2).AddDays(1).AddTicks(-1);
                    if (day == 0 || day == 5 || day == 6)
                    {
                        whereClauses.Add("start_date >= @startOfToday AND start_date <= @endOfWeekend");
                        parameters.Add(new NpgsqlParameter("startOfToday", startOfToday));
                        parameters.Add(new NpgsqlParameter("endOfWeekend", sunday));
                    }
                    else
                    {
                        whereClauses.Add("start_date >= @startOfWeekend AND start_date <= @endOfWeekend");
                        parameters.Add(new NpgsqlParameter("startOfWeekend", friday));
                        parameters.Add(new NpgsqlParameter("endOfWeekend", sunday));
                    }
                    break;
                case "month":
                    var monthEnd = startOfToday.AddDays(30).AddDays(1).AddTicks(-1);
                    whereClauses.Add("start_date >= @startOfToday AND start_date <= @monthEnd");
                    parameters.Add(new NpgsqlParameter("startOfToday", startOfToday));
                    parameters.Add(new NpgsqlParameter("monthEnd", monthEnd));
                    break;
            }
        }

        var search = (page.Search ?? string.Empty).Trim();
        if (search.Length > 0)
        {
            whereClauses.Add("(title ILIKE @search OR COALESCE(short_description, '') ILIKE @search OR COALESCE(description, '') ILIKE @search OR COALESCE(category, '') ILIKE @search OR COALESCE(tenant_name, '') ILIKE @search OR COALESCE(tenant_slug, '') ILIKE @search)");
            parameters.Add(new NpgsqlParameter("search", $"%{search}%"));
        }

        var whereSql = whereClauses.Count > 0 ? " WHERE " + string.Join(" AND ", whereClauses) : string.Empty;

        var countSql = "SELECT COUNT(*) FROM vw_events" + whereSql;
        await using (var countCmd = new NpgsqlCommand(countSql, connection))
        {
            foreach (var p in parameters)
            {
                countCmd.Parameters.Add((NpgsqlParameter)((ICloneable)p).Clone());
            }
            var totalObj = await countCmd.ExecuteScalarAsync(ct);
            response.Meta.Total = Convert.ToInt32(totalObj);
        }

        var orderSql = (request.UpcomingOnly || isPublicViewer) ? " ORDER BY start_date ASC" : " ORDER BY start_date DESC";
        var querySql = EventSelect + whereSql + orderSql + " LIMIT @lim OFFSET @off";

        await using var cmd = new NpgsqlCommand(querySql, connection);
        foreach (var p in parameters)
        {
            cmd.Parameters.Add(p);
        }
        cmd.Parameters.AddWithValue("lim", limit);
        cmd.Parameters.AddWithValue("off", offset);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Events.Add(MapEvent(reader));
        }

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
            "SELECT storage_key, image_type, is_primary, sort_order "
            + "FROM sp_link_event_image(@ev, @img, @type)", connection);
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
        var s = new MediaSettings
        {
            EventImageAspectRatio = await settings.GetStringAsync("event_image_aspect_ratio", "16:9", ct),
            EventThumbnailAspectRatio = await settings.GetStringAsync("event_thumbnail_aspect_ratio", "4:3", ct)
        };
        return s;
    }

    public override async Task<PublicAppSettings> GetPublicAppSettings(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var all = await settings.GetAllAsync(ct);
        var res = new PublicAppSettings
        {
            BookingHoldSeconds = await settings.GetIntAsync("booking_hold_seconds", 600, ct),
            DefaultTimezone = await settings.GetStringAsync("default_timezone", "America/Chicago", ct),
            EventImageAspectRatio = await settings.GetStringAsync("event_image_aspect_ratio", "16:9", ct),
            EventThumbnailAspectRatio = await settings.GetStringAsync("event_thumbnail_aspect_ratio", "4:3", ct),
            SponsorImageAspectRatio = await settings.GetStringAsync("sponsor_image_aspect_ratio", "1:1", ct),
            PerformerImageAspectRatio = await settings.GetStringAsync("performer_image_aspect_ratio", "1:1", ct),
            VenueImageAspectRatio = await settings.GetStringAsync("venue_image_aspect_ratio", "16:9", ct),
            FloorplanDefaultSize = await settings.GetIntAsync("floorplan_default_size", 80, ct),
            FloorplanCanvasWidth = await settings.GetIntAsync("floorplan_canvas_width", 1200, ct),
            FloorplanCanvasHeight = await settings.GetIntAsync("floorplan_canvas_height", 800, ct),
            FloorplanDefaultColor = await settings.GetStringAsync("floorplan_default_color", "#059669", ct)
        };
        foreach (var (k, v) in all)
        {
            res.AllSettings[k] = v;
        }
        return res;
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

    private void RequireDeveloper()
    {
        if (!tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Developer privileges required"));
        }
    }

    public override async Task<EventReminderSettings> GetEventReminderSettings(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.Value);
        await using var connection = await db.OpenBootstrapAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT events_id, reminders_enabled, reminder_1_hours, reminder_2_hours, default_reminder_1_hours, default_reminder_2_hours, reminder_7d_sent, reminder_48h_sent, last_manual_reminder_at, manual_reminder_count " +
            "FROM sp_get_event_reminder_settings(@id)", connection);
        cmd.Parameters.AddWithValue("id", eventId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new EventReminderSettings
            {
                EventsId = reader.GetGuid(0).ToString(),
                RemindersEnabled = reader.GetBoolean(1),
                Reminder1Hours = reader.GetInt32(2),
                Reminder2Hours = reader.GetInt32(3),
                DefaultReminder1Hours = reader.GetInt32(4),
                DefaultReminder2Hours = reader.GetInt32(5),
                Reminder7DSent = reader.GetBoolean(6),
                Reminder48HSent = reader.GetBoolean(7),
                LastManualReminderAt = reader.IsDBNull(8) ? 0 : new DateTimeOffset(reader.GetDateTime(8)).ToUnixTimeSeconds(),
                ManualReminderCount = reader.GetInt32(9)
            };
        }

        return new EventReminderSettings
        {
            EventsId = eventId.ToString(),
            RemindersEnabled = true,
            Reminder1Hours = 168,
            Reminder2Hours = 48,
            DefaultReminder1Hours = 168,
            DefaultReminder2Hours = 48
        };
    }

    public override async Task<AckResponse> UpdateEventReminderSettings(UpdateEventReminderSettingsRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.EventsId);
        await using var connection = await db.OpenBootstrapAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_event_reminder_settings(@id, @enabled, @r1, @r2)", connection);
        cmd.Parameters.AddWithValue("id", eventId);
        cmd.Parameters.AddWithValue("enabled", request.RemindersEnabled);
        cmd.Parameters.AddWithValue("r1", request.Reminder1Hours > 0 ? request.Reminder1Hours : DBNull.Value);
        cmd.Parameters.AddWithValue("r2", request.Reminder2Hours > 0 ? request.Reminder2Hours : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Reminder settings updated" };
    }

    public override async Task<TriggerManualEventReminderResponse> TriggerManualEventReminder(UuidValue request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.Value);
        await using var connection = await db.OpenBootstrapAsync(ct);

        var attendees = new List<(string Email, string EventTitle, DateTime StartDate, string VenueName, string VenueAddress, string TenantSlug, string TenantName)>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT email, event_title, start_date, venue_name, venue_address, tenant_slug, tenant_name " +
            "FROM sp_get_event_attendee_emails(@id)", connection))
        {
            cmd.Parameters.AddWithValue("id", eventId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                attendees.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetDateTime(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6)
                ));
            }
        }

        if (attendees.Count == 0)
        {
            return new TriggerManualEventReminderResponse
            {
                Success = true,
                Message = "No confirmed attendees found for this event",
                RecipientsCount = 0
            };
        }

        var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@ticketspan.com", ct);
        var eventLinkTemplate = await settings.GetStringAsync("event_link_base", "http://{slug}.localhost:5173/e/{eventId}", ct);

        foreach (var att in attendees)
        {
            try
            {
                var slug = string.IsNullOrEmpty(att.TenantSlug) ? "app" : att.TenantSlug;
                var eventLink = eventLinkTemplate.Replace("{slug}", slug).Replace("{eventId}", eventId.ToString());

                var values = new Dictionary<string, string>
                {
                    ["Subject"] = $"Reminder: {att.EventTitle} is coming up!",
                    ["ReminderBadge"] = "Event Reminder",
                    ["EventTitle"] = att.EventTitle,
                    ["EventDate"] = att.StartDate.ToString("f"),
                    ["VenueName"] = att.VenueName,
                    ["VenueAddress"] = string.IsNullOrEmpty(att.VenueAddress) ? "See event details for venue directions" : att.VenueAddress,
                    ["EventLink"] = eventLink,
                    ["TenantName"] = att.TenantName
                };

                var htmlBody = await templates.RenderAsync("event_reminder.html", values, ct);
                await emailService.SendAsync(fromAddress, att.Email, $"Reminder: {att.EventTitle} is coming up!", htmlBody, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send manual event reminder to {Email}", att.Email);
            }
        }

        await using (var markCmd = new NpgsqlCommand("SELECT sp_mark_event_reminder_sent(@id, 'manual')", connection))
        {
            markCmd.Parameters.AddWithValue("id", eventId);
            await markCmd.ExecuteNonQueryAsync(ct);
        }

        return new TriggerManualEventReminderResponse
        {
            Success = true,
            Message = $"Reminders dispatched to {attendees.Count} ticket holder(s)",
            RecipientsCount = attendees.Count
        };
    }
}
