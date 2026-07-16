using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Booking;

namespace TicketSpan.Api.Services;

public sealed class CheckInServiceImpl : CheckInService.CheckInServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public CheckInServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<ScanResponse> Scan(ScanRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.EventsId);
        await VerifyAccessAsync(eventId, ct);

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT success, message, guest_name, status_str FROM sp_check_in_ticket_by_token(@qr, @ev, @staff, 'qr_scan')", connection);
        cmd.Parameters.AddWithValue("qr", request.QrToken);
        cmd.Parameters.AddWithValue("ev", eventId);
        cmd.Parameters.AddWithValue("staff", tenantContext.UsersId!);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new ScanResponse { Valid = false, Message = "Ticket not found" };
        }
        return new ScanResponse
        {
            Valid = reader.GetBoolean(0),
            Message = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            HolderName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
        };
    }

    public override async Task<CheckInStats> GetCheckInStats(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.Value);
        await VerifyAccessAsync(eventId, ct);

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT total, checked_in FROM vw_event_checkin_stats WHERE events_id = @ev", connection);
        cmd.Parameters.AddWithValue("ev", eventId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new CheckInStats();
        }
        var total = reader.GetInt32(0);
        var checkedIn = reader.GetInt32(1);
        return new CheckInStats { Total = total, CheckedIn = checkedIn, Remaining = total - checkedIn };
    }

    public override async Task<ListEventsForStaffResponse> ListEventsForStaff(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListEventsForStaffResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        
        NpgsqlCommand cmd;
        if (tenantContext.IsDeveloper || tenantContext.Role == Lookups.UserRoles.Admin || tenantContext.Role == Lookups.UserRoles.SubTenant)
        {
            cmd = new NpgsqlCommand(
                "SELECT events_id, title, slug, start_date, end_date, status, venue_name FROM vw_events WHERE tenants_id = @t AND status IN ('Published', 'Completed') ORDER BY start_date", connection);
            cmd.Parameters.AddWithValue("t", tenantContext.TenantsId!);
        }
        else
        {
            cmd = new NpgsqlCommand(
                "SELECT v.events_id, v.title, v.slug, v.start_date, v.end_date, v.status, v.venue_name FROM vw_events v WHERE v.events_id IN (SELECT events_id FROM sp_list_events_for_staff(@u, 24)) ORDER BY v.start_date", connection);
            cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
        }

        await using (cmd)
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0).ToString();
                var title = reader.GetString(1);
                var slug = reader.GetString(2);
                var start = reader.GetDateTime(3);
                var end = reader.GetDateTime(4);
                var status = reader.GetString(5);
                var venueName = reader.IsDBNull(6) ? "" : reader.GetString(6);

                response.Events.Add(new StaffEvent
                {
                    EventsId = id,
                    Title = title,
                    Slug = slug,
                    StartDate = new DateTimeOffset(start, TimeSpan.Zero).ToUnixTimeSeconds(),
                    EndDate = new DateTimeOffset(end, TimeSpan.Zero).ToUnixTimeSeconds(),
                    Status = status,
                    VenueName = venueName
                });
            }
        }
        return response;
    }

    public override async Task<GetGuestListResponse> GetGuestList(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.Value);
        await VerifyAccessAsync(eventId, ct);

        var bookings = new List<GuestBooking>();
        var ticketsGrouped = new Dictionary<string, List<GuestTicket>>();

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        
        
        await using (var cmd = new NpgsqlCommand(
            @"SELECT bookings_id, booking_number, buyer_first_name, buyer_last_name, status
              FROM vw_event_guest_bookings WHERE events_id = @ev
              ORDER BY booking_number", connection))
        {
            cmd.Parameters.AddWithValue("ev", eventId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                bookings.Add(new GuestBooking
                {
                    BookingsId = reader.GetGuid(0).ToString(),
                    BookingNumber = reader.GetString(1),
                    BuyerName = $"{reader.GetString(2)} {reader.GetString(3)}",
                    Status = reader.GetString(4)
                });
            }
        }

        
        await using (var cmd = new NpgsqlCommand(
            @"SELECT booking_lines_id, bookings_id, ticket_code,
                     guest_first_name, guest_last_name, buyer_first_name, buyer_last_name,
                     status, seat_number, checked_in_time
              FROM vw_event_guest_tickets WHERE events_id = @ev
              ORDER BY seat_number", connection))
        {
            cmd.Parameters.AddWithValue("ev", eventId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var ticketsId = reader.GetGuid(0).ToString();
                var bookingsId = reader.GetGuid(1).ToString();
                var code = reader.GetString(2);
                
                string guestName;
                if (!reader.IsDBNull(3))
                {
                    guestName = $"{reader.GetString(3)} {reader.GetString(4)}";
                }
                else
                {
                    guestName = $"{reader.GetString(5)} {reader.GetString(6)}";
                }

                var status = reader.GetString(7);
                var seatNumber = reader.GetInt32(8);
                
                long checkedInAt = 0;
                if (!reader.IsDBNull(9))
                {
                    checkedInAt = new DateTimeOffset(reader.GetDateTime(9), TimeSpan.Zero).ToUnixTimeSeconds();
                }

                var ticket = new GuestTicket
                {
                    TicketsId = ticketsId,
                    TicketCode = code,
                    GuestName = guestName,
                    Status = status,
                    SeatNumber = seatNumber,
                    CheckedInAt = checkedInAt
                };

                if (!ticketsGrouped.TryGetValue(bookingsId, out var list))
                {
                    list = new List<GuestTicket>();
                    ticketsGrouped[bookingsId] = list;
                }
                list.Add(ticket);
            }
        }

        var response = new GetGuestListResponse();
        foreach (var booking in bookings)
        {
            if (ticketsGrouped.TryGetValue(booking.BookingsId, out var bTickets))
            {
                booking.Tickets.AddRange(bTickets);
            }
            response.Bookings.Add(booking);
        }

        return response;
    }

    public override async Task<ScanResponse> CheckInGuest(CheckInGuestRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.EventsId);
        await VerifyAccessAsync(eventId, ct);

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        
        bool success = false;
        string message = "Check-in failed.";
        string holderName = "";
        string status = "";

        if (request.Type == "Booking")
        {
            
            await using (var cmd = new NpgsqlCommand(
                "SELECT success, message, guest_name, status_str FROM sp_check_in_booking_by_number(@code, @ev, @staff, 'manual_entry')", connection))
            {
                cmd.Parameters.AddWithValue("code", request.CodeOrId);
                cmd.Parameters.AddWithValue("ev", eventId);
                cmd.Parameters.AddWithValue("staff", tenantContext.UsersId!);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    success = reader.GetBoolean(0);
                    message = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    holderName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    status = reader.IsDBNull(3) ? "" : reader.GetString(3);
                }
            }

            
            if (!success)
            {
                await using (var cmd = new NpgsqlCommand(
                    "SELECT success, message, guest_name, status_str FROM sp_check_in_booking_by_token(@code, @ev, @staff, 'manual_entry')", connection))
                {
                    cmd.Parameters.AddWithValue("code", request.CodeOrId);
                    cmd.Parameters.AddWithValue("ev", eventId);
                    cmd.Parameters.AddWithValue("staff", tenantContext.UsersId!);
                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        success = reader.GetBoolean(0);
                        message = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        holderName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        status = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    }
                }
            }

            
            if (!success && Guid.TryParse(request.CodeOrId, out var bookingGuid))
            {
                await using (var cmd = new NpgsqlCommand(
                    "SELECT success, message, guest_name, status_str FROM sp_check_in_booking(@id, @ev, @staff, 'manual_entry')", connection))
                {
                    cmd.Parameters.AddWithValue("id", bookingGuid);
                    cmd.Parameters.AddWithValue("ev", eventId);
                    cmd.Parameters.AddWithValue("staff", tenantContext.UsersId!);
                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        success = reader.GetBoolean(0);
                        message = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        holderName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        status = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    }
                }
            }
        }
        else 
        {
            
            await using (var cmd = new NpgsqlCommand(
                "SELECT success, message, guest_name, status_str FROM sp_check_in_ticket_by_code(@code, @ev, @staff, 'manual_entry')", connection))
            {
                cmd.Parameters.AddWithValue("code", request.CodeOrId);
                cmd.Parameters.AddWithValue("ev", eventId);
                cmd.Parameters.AddWithValue("staff", tenantContext.UsersId!);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    success = reader.GetBoolean(0);
                    message = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    holderName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    status = reader.IsDBNull(3) ? "" : reader.GetString(3);
                }
            }

            
            if (!success)
            {
                await using (var cmd = new NpgsqlCommand(
                    "SELECT success, message, guest_name, status_str FROM sp_check_in_ticket_by_token(@code, @ev, @staff, 'manual_entry')", connection))
                {
                    cmd.Parameters.AddWithValue("code", request.CodeOrId);
                    cmd.Parameters.AddWithValue("ev", eventId);
                    cmd.Parameters.AddWithValue("staff", tenantContext.UsersId!);
                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        success = reader.GetBoolean(0);
                        message = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        holderName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        status = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    }
                }
            }

            
            if (!success && Guid.TryParse(request.CodeOrId, out var ticketGuid))
            {
                await using (var cmd = new NpgsqlCommand(
                    "SELECT success, message, guest_name, status_str FROM sp_check_in_ticket(@id, @ev, @staff, 'manual_entry')", connection))
                {
                    cmd.Parameters.AddWithValue("id", ticketGuid);
                    cmd.Parameters.AddWithValue("ev", eventId);
                    cmd.Parameters.AddWithValue("staff", tenantContext.UsersId!);
                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        success = reader.GetBoolean(0);
                        message = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        holderName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        status = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    }
                }
            }
        }

        return new ScanResponse
        {
            Valid = success,
            Message = message,
            HolderName = holderName,
            Status = status
        };
    }

    public override async Task<LookupBookingResponse> LookupBooking(CheckInGuestRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.EventsId);
        await VerifyAccessAsync(eventId, ct);

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        Guid bookingId;
        await using (var cmd = new NpgsqlCommand(
            "SELECT bookings_id FROM sp_lookup_booking_for_checkin(@code, @ev)", connection))
        {
            cmd.Parameters.AddWithValue("code", request.CodeOrId);
            cmd.Parameters.AddWithValue("ev", eventId);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is not Guid found)
            {
                return new LookupBookingResponse { Found = false, Message = "Booking not found" };
            }
            bookingId = found;
        }

        GuestBooking? booking = null;
        await using (var cmd = new NpgsqlCommand(
            @"SELECT bookings_id, booking_number, buyer_first_name, buyer_last_name, status
              FROM vw_event_guest_bookings WHERE events_id = @ev AND bookings_id = @b", connection))
        {
            cmd.Parameters.AddWithValue("ev", eventId);
            cmd.Parameters.AddWithValue("b", bookingId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                booking = new GuestBooking
                {
                    BookingsId = reader.GetGuid(0).ToString(),
                    BookingNumber = reader.GetString(1),
                    BuyerName = $"{reader.GetString(2)} {reader.GetString(3)}",
                    Status = reader.GetString(4)
                };
            }
        }

        if (booking is null)
        {
            return new LookupBookingResponse { Found = false, Message = "Booking not found" };
        }

        await using (var cmd = new NpgsqlCommand(
            @"SELECT booking_lines_id, ticket_code,
                     guest_first_name, guest_last_name, buyer_first_name, buyer_last_name,
                     status, seat_number, checked_in_time
              FROM vw_event_guest_tickets WHERE events_id = @ev AND bookings_id = @b
              ORDER BY seat_number", connection))
        {
            cmd.Parameters.AddWithValue("ev", eventId);
            cmd.Parameters.AddWithValue("b", bookingId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                string guestName;
                if (!reader.IsDBNull(2))
                {
                    guestName = $"{reader.GetString(2)} {reader.GetString(3)}";
                }
                else
                {
                    guestName = $"{reader.GetString(4)} {reader.GetString(5)}";
                }

                long checkedInAt = 0;
                if (!reader.IsDBNull(8))
                {
                    checkedInAt = new DateTimeOffset(reader.GetDateTime(8), TimeSpan.Zero).ToUnixTimeSeconds();
                }

                booking.Tickets.Add(new GuestTicket
                {
                    TicketsId = reader.GetGuid(0).ToString(),
                    TicketCode = reader.GetString(1),
                    GuestName = guestName,
                    Status = reader.GetString(6),
                    SeatNumber = reader.GetInt32(7),
                    CheckedInAt = checkedInAt
                });
            }
        }

        return new LookupBookingResponse { Found = true, Booking = booking };
    }

    public override async Task<ScanResponse> UncheckInTicket(UncheckInTicketRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.EventsId);
        await VerifyAccessAsync(eventId, ct);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A reason is required to undo a check-in"));
        }

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT success, message, guest_name, status_str FROM sp_uncheck_in_ticket(@id, @ev, @staff, @reason)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.TicketsId));
        cmd.Parameters.AddWithValue("ev", eventId);
        cmd.Parameters.AddWithValue("staff", tenantContext.UsersId!);
        cmd.Parameters.AddWithValue("reason", request.Reason.Trim());
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new ScanResponse { Valid = false, Message = "Undo check-in failed" };
        }
        return new ScanResponse
        {
            Valid = reader.GetBoolean(0),
            Message = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            HolderName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
        };
    }

    public override async Task<ListCheckInLogsResponse> ListCheckInLogs(ListCheckInLogsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var eventId = Guid.Parse(request.EventsId);
        await VerifyAccessAsync(eventId, ct);

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 50;
        var offset = request.Page > 0 ? (request.Page - 1) * pageSize : 0;

        var response = new ListCheckInLogsResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT checkin_logs_id, staff_name, attendee_name, booking_number, ticket_code, ticket_type_label, "
            + "timestamp, method, status, failure_reason, COUNT(*) OVER() "
            + "FROM vw_checkin_logs WHERE events_id = @ev "
            + "AND (@staff IS NULL OR staff_user_id = @staff) "
            + "AND (@method IS NULL OR method = @method) "
            + "AND (@status IS NULL OR status = @status) "
            + "ORDER BY timestamp DESC LIMIT @limit OFFSET @offset", connection);
        cmd.Parameters.AddWithValue("ev", eventId);
        cmd.Parameters.Add(new NpgsqlParameter("staff", NpgsqlTypes.NpgsqlDbType.Uuid)
        {
            Value = string.IsNullOrEmpty(request.StaffUserId) ? DBNull.Value : Guid.Parse(request.StaffUserId)
        });
        cmd.Parameters.Add(new NpgsqlParameter("method", NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = string.IsNullOrEmpty(request.Method) ? DBNull.Value : request.Method
        });
        cmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = string.IsNullOrEmpty(request.Status) ? DBNull.Value : request.Status
        });
        cmd.Parameters.AddWithValue("limit", pageSize);
        cmd.Parameters.AddWithValue("offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.TotalCount = (int)reader.GetInt64(10);
            response.Logs.Add(new CheckInLogEntry
            {
                CheckinLogsId = reader.GetGuid(0).ToString(),
                StaffName = reader.GetString(1),
                AttendeeName = reader.GetString(2),
                BookingNumber = reader.GetString(3),
                TicketCode = reader.GetString(4),
                TicketTypeLabel = reader.GetString(5),
                Timestamp = new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc)).ToUnixTimeSeconds(),
                Method = reader.GetString(7),
                Status = reader.GetString(8),
                FailureReason = reader.GetString(9)
            });
        }
        return response;
    }

    private async Task VerifyAccessAsync(Guid eventId, CancellationToken ct)
    {
        if (tenantContext.IsDeveloper || tenantContext.Role == Lookups.UserRoles.Admin || tenantContext.Role == Lookups.UserRoles.SubTenant)
        {
            return;
        }
        if (tenantContext.Role == Lookups.UserRoles.Staff)
        {
            
            await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT sp_staff_can_access_event(@u, @ev)", connection);
            cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
            cmd.Parameters.AddWithValue("ev", eventId);
            var allowed = (bool)(await cmd.ExecuteScalarAsync(ct))!;
            if (allowed) return;
        }
        if (tenantContext.Role == Lookups.UserRoles.EventManager)
        {
            
            await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
            await using var cmd = new NpgsqlCommand("SELECT app.can_access_event(@ev)", connection);
            cmd.Parameters.AddWithValue("ev", eventId);
            if (await cmd.ExecuteScalarAsync(ct) is true) return;
        }
        throw new RpcException(new Status(StatusCode.PermissionDenied, "Not Authorized"));
    }
}
