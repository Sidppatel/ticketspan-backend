using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Booking;

namespace Svyne.Api.Services;

public sealed class BookingServiceImpl : BookingService.BookingServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public BookingServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<CreateBookingResponse> CreateBooking(CreateBookingRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT bookings_id, booking_number FROM sp_create_booking(@u, @ev, @tbl, @seats, @tt, @sub, @fee, @total, 'Pending')", connection);
        cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("tbl", string.IsNullOrEmpty(request.TablesId) ? DBNull.Value : Guid.Parse(request.TablesId));
        cmd.Parameters.AddWithValue("seats", request.Seats == 0 ? DBNull.Value : request.Seats);
        cmd.Parameters.AddWithValue("tt", string.IsNullOrEmpty(request.EventTicketTypesId) ? DBNull.Value : Guid.Parse(request.EventTicketTypesId));
        cmd.Parameters.AddWithValue("sub", request.SubtotalCents);
        cmd.Parameters.AddWithValue("fee", request.FeeCents);
        cmd.Parameters.AddWithValue("total", request.TotalCents);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new CreateBookingResponse { BookingsId = reader.GetGuid(0).ToString(), BookingNumber = reader.GetString(1) };
    }

    public override async Task<CreateBookingResponse> ReserveOpenCapacity(ReserveOpenCapacityRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT bookings_id, booking_number FROM sp_reserve_open_capacity(@u, @ev, @seats, @tt, @sub, @fee, @total)", connection);
        cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("seats", request.Seats);
        cmd.Parameters.AddWithValue("tt", string.IsNullOrEmpty(request.EventTicketTypesId) ? DBNull.Value : Guid.Parse(request.EventTicketTypesId));
        cmd.Parameters.AddWithValue("sub", request.SubtotalCents);
        cmd.Parameters.AddWithValue("fee", request.FeeCents);
        cmd.Parameters.AddWithValue("total", request.TotalCents);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new CreateBookingResponse { BookingsId = reader.GetGuid(0).ToString(), BookingNumber = reader.GetString(1) };
    }

    public override Task<AckResponse> ConfirmBooking(ConfirmBookingRequest request, ServerCallContext context)
        => RunVoid("SELECT sp_confirm_booking(@id, @qr)", request.BookingsId, context, ("qr", request.QrToken), "Booking confirmed");

    public override Task<AckResponse> CancelBooking(UuidValue request, ServerCallContext context)
        => RunVoid("SELECT sp_cancel_booking(@id)", request.Value, context, null, "Booking cancelled");

    public override Task<AckResponse> RefundBooking(UuidValue request, ServerCallContext context)
        => RunVoid("SELECT sp_refund_booking(@id)", request.Value, context, null, "Booking refunded");

    public override async Task<BookingStats> GetBookingStats(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT total, paid, checked_in, revenue FROM sp_get_booking_stats(NULL, @ev)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new BookingStats();
        }
        return new BookingStats
        {
            Total = reader.GetInt32(0),
            Paid = reader.GetInt32(1),
            CheckedIn = reader.GetInt32(2),
            RevenueCents = reader.GetInt64(3)
        };
    }

    private async Task<AckResponse> RunVoid(string sql, string id, ServerCallContext context, (string, string)? extra, string okMessage)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(id));
        if (extra is { } e)
        {
            cmd.Parameters.AddWithValue(e.Item1, e.Item2);
        }
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = okMessage };
    }

    public override async Task<Booking> GetBooking(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(BookingSelect + " WHERE bookings_id = @id", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
        }
        return MapBooking(reader);
    }

    public override async Task<ListBookingsResponse> ListBookings(ListBookingsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var page = request.Page ?? new PageRequest();
        var response = new ListBookingsResponse { Meta = new PageMeta { Offset = page.Offset, Limit = page.Limit } };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            BookingSelect + " WHERE (@ev = '00000000-0000-0000-0000-000000000000' OR events_id = @ev) "
            + "AND (@status = '' OR status = @status) ORDER BY created_at DESC LIMIT @lim OFFSET @off", connection);
        cmd.Parameters.AddWithValue("ev", string.IsNullOrEmpty(request.EventsId) ? Guid.Empty : Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("status", request.Status ?? string.Empty);
        cmd.Parameters.AddWithValue("lim", page.Limit <= 0 ? 25 : page.Limit);
        cmd.Parameters.AddWithValue("off", page.Offset);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Bookings.Add(MapBooking(reader));
        }
        response.Meta.Total = response.Bookings.Count;
        return response;
    }

    private const string BookingSelect =
        "SELECT bookings_id, booking_number, status, users_id, events_id, subtotal_cents, fee_cents, total_cents, "
        + "COALESCE(seats_reserved, 0) FROM vw_bookings";

    private static Booking MapBooking(NpgsqlDataReader r) => new()
    {
        BookingsId = r.GetGuid(0).ToString(),
        BookingNumber = r.GetString(1),
        Status = r.GetString(2),
        UsersId = r.IsDBNull(3) ? string.Empty : r.GetGuid(3).ToString(),
        EventsId = r.IsDBNull(4) ? string.Empty : r.GetGuid(4).ToString(),
        SubtotalCents = r.GetInt32(5),
        FeeCents = r.GetInt32(6),
        TotalCents = r.GetInt32(7),
        SeatsReserved = r.GetInt32(8)
    };

    private void RequireUser()
    {
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
    }
}
