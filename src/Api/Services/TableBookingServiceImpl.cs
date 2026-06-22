using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Booking;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class TableBookingServiceImpl : TableBookingService.TableBookingServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public TableBookingServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<EventLayout> GetEventLayout(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventsId = Guid.Parse(request.Value);
        var layout = new EventLayout { EventsId = request.Value };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using (var gridCmd = new NpgsqlCommand("SELECT COALESCE(grid_rows,0), COALESCE(grid_cols,0) FROM events WHERE events_id = @ev", connection))
        {
            gridCmd.Parameters.AddWithValue("ev", eventsId);
            await using var gridReader = await gridCmd.ExecuteReaderAsync(ct);
            if (await gridReader.ReadAsync(ct))
            {
                layout.GridRows = gridReader.GetInt32(0);
                layout.GridCols = gridReader.GetInt32(1);
            }
        }
        await using var cmd = new NpgsqlCommand(
            "SELECT tables_id, event_tables_id, label, grid_row, grid_col, row_span, col_span, status "
            + "FROM tables WHERE events_id = @ev ORDER BY sort_order", connection);
        cmd.Parameters.AddWithValue("ev", eventsId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            layout.Tables.Add(new Table
            {
                TablesId = reader.GetGuid(0).ToString(),
                EventTablesId = reader.GetGuid(1).ToString(),
                Label = reader.GetString(2),
                GridRow = reader.GetInt32(3),
                GridCol = reader.GetInt32(4),
                RowSpan = reader.GetInt32(5),
                ColSpan = reader.GetInt32(6),
                Status = reader.GetString(7)
            });
        }
        return layout;
    }

    public override async Task<ListTablesResponse> ListTablesForEvent(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListTablesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT tables_id, event_tables_id, label, grid_row, grid_col, row_span, col_span, status "
            + "FROM tables WHERE events_id = @ev ORDER BY sort_order", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Tables.Add(new Table
            {
                TablesId = reader.GetGuid(0).ToString(),
                EventTablesId = reader.GetGuid(1).ToString(),
                Label = reader.GetString(2),
                GridRow = reader.GetInt32(3),
                GridCol = reader.GetInt32(4),
                RowSpan = reader.GetInt32(5),
                ColSpan = reader.GetInt32(6),
                Status = reader.GetString(7)
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
            "SELECT sp_save_event_layout(@ev, @rows, @cols, @tables::jsonb, @locked)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("rows", request.GridRows);
        cmd.Parameters.AddWithValue("cols", request.GridCols);
        cmd.Parameters.AddWithValue("tables", string.IsNullOrEmpty(request.TablesJson) ? "[]" : request.TablesJson);
        cmd.Parameters.AddWithValue("locked", request.LockedIds.Select(Guid.Parse).ToArray());
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Layout saved" };
    }

    public override async Task<AckResponse> LockTable(LockTableRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT id FROM sp_lock_table(@u, (SELECT events_id FROM tables WHERE tables_id = @t), @t, 15)", connection);
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
            "SELECT sp_release_table_lock(@u, (SELECT events_id FROM tables WHERE tables_id = @t), @t)", connection);
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
            "SELECT sp_create_event_table(@ev, @label, @cap, @shape, @color, @price, @fee, @tpl)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("label", request.Label);
        cmd.Parameters.AddWithValue("cap", request.Capacity);
        cmd.Parameters.AddWithValue("shape", string.IsNullOrEmpty(request.Shape) ? "Round" : request.Shape);
        cmd.Parameters.AddWithValue("color", (object?)NullIfEmpty(request.Color) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("price", request.PriceCents);
        cmd.Parameters.AddWithValue("fee", request.PlatformFeeCents == 0 ? DBNull.Value : request.PlatformFeeCents);
        cmd.Parameters.AddWithValue("tpl", string.IsNullOrEmpty(request.TableTemplatesId) ? DBNull.Value : Guid.Parse(request.TableTemplatesId));
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
            "SELECT sp_create_event_ticket_type(@ev, @label, @price, @fee, @max, @sort, @desc)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("label", request.Label);
        cmd.Parameters.AddWithValue("price", request.PriceCents);
        cmd.Parameters.AddWithValue("fee", request.PlatformFeeCents == 0 ? DBNull.Value : request.PlatformFeeCents);
        cmd.Parameters.AddWithValue("max", request.MaxQuantity == 0 ? DBNull.Value : request.MaxQuantity);
        cmd.Parameters.AddWithValue("sort", request.SortOrder);
        cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.Description) ?? DBNull.Value);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override Task<AckResponse> DeleteEventTicketType(UuidValue request, ServerCallContext context)
        => RunVoid("SELECT sp_delete_event_ticket_type(@id)", request.Value, context, "Ticket type deleted");

    private async Task<AckResponse> RunVoid(string sql, string id, ServerCallContext context, string okMessage)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(id));
        await cmd.ExecuteNonQueryAsync(ct);
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
