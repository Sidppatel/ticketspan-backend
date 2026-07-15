using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Booking;
using TicketSpan.Protos.Common;

namespace TicketSpan.Api.Services;

public sealed class TableBookingServiceImpl : TableBookingService.TableBookingServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public TableBookingServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    
    
    
    private static Table MapTable(NpgsqlDataReader r) => new()
    {
        TablesId = r.GetGuid(0).ToString(),
        EventTablesId = r.GetGuid(1).ToString(),
        Label = r.GetString(2),
        PosX = (double)r.GetDecimal(3),
        PosY = (double)r.GetDecimal(4),
        Width = (double)r.GetDecimal(5),
        Height = (double)r.GetDecimal(6),
        Status = r.GetString(7),
        PriceCents = r.GetInt32(8),
        PlatformFeeCents = r.GetInt32(9),
        FeeFormulasId = r.IsDBNull(10) ? string.Empty : r.GetGuid(10).ToString(),
        ShapeOverride = r.IsDBNull(11) ? string.Empty : r.GetString(11),
        ColorOverride = r.IsDBNull(12) ? string.Empty : r.GetString(12),
        CapacityOverride = r.IsDBNull(13) ? 0 : r.GetInt32(13),
        PricesId = r.IsDBNull(14) ? string.Empty : r.GetGuid(14).ToString()
    };

    private const string TableSelect =
        "SELECT tables_id, event_tables_id, label, pos_x, pos_y, width, height, status, "
        + "price_cents, platform_fee_cents, fee_formulas_id, "
        + "shape_override, color_override, capacity_override, prices_id "
        + "FROM vw_event_layout_tables WHERE events_id = @ev ORDER BY sort_order";

    public override async Task<EventLayout> GetEventLayout(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventsId = Guid.Parse(request.Value);
        var layout = new EventLayout { EventsId = request.Value };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await EventAccess.RequireAsync(connection, tenantContext, eventsId, ct);
        await using var cmd = new NpgsqlCommand(TableSelect, connection);
        cmd.Parameters.AddWithValue("ev", eventsId);
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                layout.Tables.Add(MapTable(reader));
            }
        }
        await using (var objCmd = new NpgsqlCommand("SELECT layout_objects_id, object_type, label, pos_x, pos_y, width, height, color, sort_order FROM sp_list_layout_objects_for_event(@ev)", connection))
        {
            objCmd.Parameters.AddWithValue("ev", eventsId);
            await using var objReader = await objCmd.ExecuteReaderAsync(ct);
            while (await objReader.ReadAsync(ct))
            {
                layout.Objects.Add(new LayoutObject
                {
                    LayoutObjectsId = objReader.GetGuid(0).ToString(),
                    ObjectType = objReader.GetString(1),
                    Label = objReader.IsDBNull(2) ? string.Empty : objReader.GetString(2),
                    PosX = (double)objReader.GetDecimal(3),
                    PosY = (double)objReader.GetDecimal(4),
                    Width = (double)objReader.GetDecimal(5),
                    Height = (double)objReader.GetDecimal(6),
                    Color = objReader.IsDBNull(7) ? string.Empty : objReader.GetString(7),
                    SortOrder = objReader.GetInt32(8)
                });
            }
        }
        return layout;
    }

    public override async Task<ListTablesResponse> ListTablesForEvent(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListTablesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await EventAccess.RequireAsync(connection, tenantContext, Guid.Parse(request.Value), ct);
        await using var cmd = new NpgsqlCommand(TableSelect, connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Tables.Add(MapTable(reader));
        }
        return response;
    }

    public override async Task<ListEventTableTypesResponse> ListEventTableTypes(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListEventTableTypesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await EventAccess.RequireAsync(connection, tenantContext, Guid.Parse(request.Value), ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT event_tables_id, label, capacity, shape, color, price_cents, prices_id, "
            + "default_width, default_height, platform_fee_cents "
            + "FROM vw_event_table_types WHERE events_id = @ev AND is_active = true ORDER BY label", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.TableTypes.Add(new EventTableType
            {
                EventTablesId = reader.GetGuid(0).ToString(),
                Label = reader.GetString(1),
                Capacity = reader.GetInt32(2),
                Shape = reader.GetString(3),
                Color = reader.GetString(4),
                PriceCents = reader.GetInt32(5),
                PricesId = reader.IsDBNull(6) ? string.Empty : reader.GetGuid(6).ToString(),
                DefaultWidth = (double)reader.GetDecimal(7),
                DefaultHeight = (double)reader.GetDecimal(8),
                PlatformFeeCents = reader.GetInt32(9)
            });
        }
        return response;
    }

    public override async Task<AckResponse> SaveEventLayout(SaveEventLayoutRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_save_event_layout(@ev, @tables::jsonb, @locked, @objects::jsonb)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("tables", string.IsNullOrEmpty(request.TablesJson) ? "[]" : request.TablesJson);
        cmd.Parameters.AddWithValue("locked", request.LockedIds.Select(Guid.Parse).ToArray());
        cmd.Parameters.AddWithValue("objects", string.IsNullOrEmpty(request.ObjectsJson) ? "[]" : request.ObjectsJson);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Layout saved" };
    }

    public override async Task<AckResponse> LockTable(LockTableRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT id FROM sp_lock_table(@u, (SELECT events_id FROM vw_event_layout_tables WHERE tables_id = @t), @t, 15)", connection);
        cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
        cmd.Parameters.AddWithValue("t", Guid.Parse(request.TablesId));
        var result = await cmd.ExecuteScalarAsync(ct);
        return new AckResponse { Success = result is Guid, Message = result is Guid ? "Table locked" : "Lock failed" };
    }

    public override async Task<AckResponse> ReleaseTableLock(LockTableRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_release_table_lock(@u, (SELECT events_id FROM vw_event_layout_tables WHERE tables_id = @t), @t)", connection);
        cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
        cmd.Parameters.AddWithValue("t", Guid.Parse(request.TablesId));
        var ok = (bool)(await cmd.ExecuteScalarAsync(ct))!;
        return new AckResponse { Success = ok, Message = ok ? "Lock released" : "Release failed" };
    }

    public override async Task<UuidValue> CreateEventTable(CreateEventTableRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_event_table(@ev, @label, @cap, @shape, @color, @price, @fee, @tpl, @allinc, @per, @width, @height)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("label", request.Label);
        cmd.Parameters.AddWithValue("cap", request.Capacity);
        
        
        cmd.Parameters.AddWithValue("shape", (object?)NullIfEmpty(request.Shape) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("color", (object?)NullIfEmpty(request.Color) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("price", request.PriceCents);
        cmd.Parameters.AddWithValue("fee", string.IsNullOrEmpty(request.FeeFormulasId) ? DBNull.Value : Guid.Parse(request.FeeFormulasId));
        cmd.Parameters.AddWithValue("tpl", string.IsNullOrEmpty(request.TableTemplatesId) ? DBNull.Value : Guid.Parse(request.TableTemplatesId));
        cmd.Parameters.AddWithValue("allinc", request.IsAllInclusive);
        cmd.Parameters.AddWithValue("per", request.PerAttendeeCents);
        cmd.Parameters.AddWithValue("width", request.Width <= 0 ? DBNull.Value : (decimal)request.Width);
        cmd.Parameters.AddWithValue("height", request.Height <= 0 ? DBNull.Value : (decimal)request.Height);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override Task<AckResponse> DeleteEventTable(UuidValue request, ServerCallContext context)
        => RunVoid("SELECT sp_delete_event_table(@id)", request.Value, context, "Event table deleted");

    public override async Task<UuidValue> CreateEventTicketType(CreateEventTicketTypeRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_event_ticket_type(@ev, @label, @price, @fee, @max, @sort, @cap, @desc)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("label", request.Label);
        cmd.Parameters.AddWithValue("price", request.PriceCents);
        cmd.Parameters.AddWithValue("fee", string.IsNullOrEmpty(request.FeeFormulasId) ? DBNull.Value : Guid.Parse(request.FeeFormulasId));
        cmd.Parameters.AddWithValue("max", request.MaxQuantity == 0 ? DBNull.Value : request.MaxQuantity);
        cmd.Parameters.AddWithValue("sort", request.SortOrder);
        cmd.Parameters.AddWithValue("cap", request.Capacity == 0 ? DBNull.Value : request.Capacity);
        cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.Description) ?? DBNull.Value);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<AckResponse> UpdateEventTicketType(UpdateEventTicketTypeRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_event_ticket_type(@id, @label, @price, @fee, @max, @sort, @cap, @active, @desc)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.EventTicketTypesId));
        cmd.Parameters.AddWithValue("label", request.Label);
        cmd.Parameters.AddWithValue("price", request.PriceCents);
        cmd.Parameters.AddWithValue("fee", string.IsNullOrEmpty(request.FeeFormulasId) ? DBNull.Value : Guid.Parse(request.FeeFormulasId));
        cmd.Parameters.AddWithValue("max", request.MaxQuantity == 0 ? DBNull.Value : request.MaxQuantity);
        cmd.Parameters.AddWithValue("sort", request.SortOrder);
        cmd.Parameters.AddWithValue("cap", request.Capacity == 0 ? DBNull.Value : request.Capacity);
        cmd.Parameters.AddWithValue("active", request.IsActive);
        cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.Description) ?? DBNull.Value);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex)
        {
            throw MapSaleLockConflict(ex);
        }
        return new AckResponse { Success = true, Message = "Ticket type updated" };
    }

    public override Task<AckResponse> DeleteEventTicketType(UuidValue request, ServerCallContext context)
        => RunVoid("SELECT sp_delete_event_ticket_type(@id)", request.Value, context, "Ticket type deleted");

    public override async Task<UuidValue> CreateTableTemplatePriceRule(CreateTableTemplatePriceRuleRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_table_template_price_rule(@tpl, @name, @type, @prio, @price, @from, @until, @min, @max)", connection);
        cmd.Parameters.AddWithValue("tpl", Guid.Parse(request.TableTemplatesId));
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("type", string.IsNullOrEmpty(request.RuleType) ? "TimeWindow" : request.RuleType);
        cmd.Parameters.AddWithValue("prio", request.Priority);
        cmd.Parameters.AddWithValue("price", request.PriceCents);
        cmd.Parameters.AddWithValue("from", ToTimestamp(request.ActiveFrom));
        cmd.Parameters.AddWithValue("until", ToTimestamp(request.ActiveUntil));
        cmd.Parameters.AddWithValue("min", request.MinRemaining < 0 ? DBNull.Value : request.MinRemaining);
        cmd.Parameters.AddWithValue("max", request.MaxRemaining < 0 ? DBNull.Value : request.MaxRemaining);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<ListTableTemplatePriceRulesResponse> ListTableTemplatePriceRules(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var response = new ListTableTemplatePriceRulesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT table_template_price_rules_id, table_templates_id, name, rule_type, priority, price_cents, active_from, active_until, min_remaining, max_remaining, is_active FROM sp_list_table_template_price_rules(@tpl)", connection);
        cmd.Parameters.AddWithValue("tpl", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Rules.Add(new TableTemplatePriceRule
            {
                TableTemplatePriceRulesId = reader.GetGuid(0).ToString(),
                TableTemplatesId = reader.GetGuid(1).ToString(),
                Name = reader.GetString(2),
                RuleType = reader.GetString(3),
                Priority = reader.GetInt32(4),
                PriceCents = reader.GetInt32(5),
                ActiveFrom = FromTimestamp(reader, 6),
                ActiveUntil = FromTimestamp(reader, 7),
                MinRemaining = reader.IsDBNull(8) ? -1 : reader.GetInt32(8),
                MaxRemaining = reader.IsDBNull(9) ? -1 : reader.GetInt32(9),
                IsActive = reader.GetBoolean(10)
            });
        }
        return response;
    }

    public override Task<AckResponse> DeleteTableTemplatePriceRule(UuidValue request, ServerCallContext context)
        => RunVoid("SELECT sp_delete_table_template_price_rule(@id)", request.Value, context, "Template price rule deleted");

    public override async Task<ListTableTemplatesResponse> ListTableTemplates(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var response = new ListTableTemplatesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT table_templates_id, name, default_capacity, default_shape, "
            + "default_color, default_price_cents, is_active, "
            + "default_width, default_height, default_is_all_inclusive "
            + "FROM vw_table_templates ORDER BY name", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Templates.Add(new TableTemplate
            {
                TableTemplatesId = reader.GetGuid(0).ToString(),
                Name = reader.GetString(1),
                DefaultCapacity = reader.GetInt32(2),
                DefaultShape = reader.GetString(3),
                DefaultColor = reader.GetString(4),
                DefaultPriceCents = reader.GetInt32(5),
                IsActive = reader.GetBoolean(6),
                DefaultWidth = (double)reader.GetDecimal(7),
                DefaultHeight = (double)reader.GetDecimal(8),
                DefaultIsAllInclusive = reader.GetBoolean(9)
            });
        }
        return response;
    }

    public override async Task<UuidValue> CreateTableTemplate(CreateTableTemplateRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        if (tenantContext.TenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_table_template(@t, @name, @cap, @shape, @color, @price, @width, @height, @inc)", connection);
        cmd.Parameters.AddWithValue("t", tenantContext.TenantsId);
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("cap", request.DefaultCapacity);
        cmd.Parameters.AddWithValue("shape", string.IsNullOrEmpty(request.DefaultShape) ? "Round" : request.DefaultShape);
        cmd.Parameters.AddWithValue("color", (object?)NullIfEmpty(request.DefaultColor) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("price", request.DefaultPriceCents);
        cmd.Parameters.AddWithValue("width", request.DefaultWidth <= 0 ? 80m : (decimal)request.DefaultWidth);
        cmd.Parameters.AddWithValue("height", request.DefaultHeight <= 0 ? 80m : (decimal)request.DefaultHeight);
        cmd.Parameters.AddWithValue("inc", request.DefaultIsAllInclusive);
        try
        {
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            return new UuidValue { Value = id.ToString() };
        }
        catch (PostgresException ex)
        {
            throw MapTemplateConflict(ex);
        }
    }

    public override async Task<AckResponse> UpdateTableTemplate(UpdateTableTemplateRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_table_template(@id, NULL, @cap, @shape, @color, @price, @active, @width, @height, @inc)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.TableTemplatesId));
        cmd.Parameters.AddWithValue("cap", request.DefaultCapacity);
        cmd.Parameters.AddWithValue("shape", string.IsNullOrEmpty(request.DefaultShape) ? "Round" : request.DefaultShape);
        cmd.Parameters.AddWithValue("color", (object?)NullIfEmpty(request.DefaultColor) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("price", request.DefaultPriceCents);
        cmd.Parameters.AddWithValue("active", request.IsActive);
        cmd.Parameters.AddWithValue("width", request.DefaultWidth <= 0 ? 80m : (decimal)request.DefaultWidth);
        cmd.Parameters.AddWithValue("height", request.DefaultHeight <= 0 ? 80m : (decimal)request.DefaultHeight);
        cmd.Parameters.AddWithValue("inc", request.DefaultIsAllInclusive);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
            return new AckResponse { Success = true, Message = "Table template updated" };
        }
        catch (PostgresException ex)
        {
            throw MapTemplateConflict(ex);
        }
    }

    public override Task<AckResponse> DeleteTableTemplate(UuidValue request, ServerCallContext context)
        => RunVoid("SELECT sp_deactivate_table_template(@id)", request.Value, context, "Table template deactivated");

    private static RpcException MapSaleLockConflict(PostgresException ex) => ex.SqlState == "P0001"
        ? new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText))
        : new RpcException(new Status(StatusCode.Internal, ex.MessageText));

    private static RpcException MapTemplateConflict(PostgresException ex) => ex.SqlState == "23505"
        ? new RpcException(new Status(StatusCode.AlreadyExists,
            ex.ConstraintName?.Contains("color") == true
                ? "A table type with this color already exists"
                : "A table type with this name already exists"))
        : new RpcException(new Status(StatusCode.Internal, ex.MessageText));

    private static object ToTimestamp(long unixSeconds) =>
        unixSeconds <= 0 ? DBNull.Value : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

    private static long FromTimestamp(NpgsqlDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? 0 : new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(ordinal), DateTimeKind.Utc)).ToUnixTimeSeconds();

    private async Task<AckResponse> RunVoid(string sql, string id, ServerCallContext context, string okMessage)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(id));
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex)
        {
            throw MapSaleLockConflict(ex);
        }
        return new AckResponse { Success = true, Message = okMessage };
    }

    private void RequireTenant()
    {
        if (tenantContext.TenantsId is null && !tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
    }

    private void RequireUser()
    {
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
