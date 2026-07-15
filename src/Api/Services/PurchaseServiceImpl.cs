using Grpc.Core;
using Npgsql;
using Stripe;
using TicketSpan.Api.Data;
using TicketSpan.Api.Payments;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Booking;
using TicketSpan.Protos.Pricing;

using TicketSpan.Api.Email;

namespace TicketSpan.Api.Services;

public sealed partial class BookingServiceImpl : BookingService.BookingServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly StripeService stripe;
    private readonly SalesTaxService taxRates;
    private readonly IEmailService email;
    private readonly EmailTemplateRenderer templates;
    private readonly AppSettingsProvider settings;
    private readonly ILogger<BookingServiceImpl> logger;

    public BookingServiceImpl(
        Db db,
        TenantContext tenantContext,
        StripeService stripe,
        SalesTaxService taxRates,
        IEmailService email,
        EmailTemplateRenderer templates,
        AppSettingsProvider settings,
        ILogger<BookingServiceImpl> logger)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.stripe = stripe;
        this.taxRates = taxRates;
        this.email = email;
        this.templates = templates;
        this.settings = settings;
        this.logger = logger;
    }

    public override async Task<ListEventTicketTypesResponse> ListEventTicketTypes(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListEventTicketTypesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT event_ticket_types_id, label, price_cents, platform_fee_cents, "
            + "max_quantity, description, fee_formulas_id, capacity, "
            + "selling_price_cents, sold_count, service_fee_cents, tax_cents, total_cents "
            + "FROM vw_event_ticket_types_pricing "
            + "WHERE events_id = @ev AND is_active = true ORDER BY sort_order, label", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.TicketTypes.Add(new EventTicketType
            {
                EventTicketTypesId = reader.GetGuid(0).ToString(),
                Label = reader.GetString(1),
                PriceCents = reader.GetInt32(2),
                PlatformFeeCents = reader.GetInt32(3),
                MaxQuantity = reader.GetInt32(4),
                Description = reader.GetString(5),
                FeeFormulasId = reader.IsDBNull(6) ? string.Empty : reader.GetGuid(6).ToString(),
                Capacity = reader.GetInt32(7),
                SellingPriceCents = reader.GetInt32(8),
                SoldCount = reader.GetInt32(9),
                ServiceFeeCents = reader.GetInt32(10),
                TaxCents = reader.GetInt32(11),
                TotalCents = reader.GetInt32(12)
            });
        }
        return response;
    }

    public override async Task<CreateBookingResponse> CreateBooking(CreateBookingRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await taxRates.EnsureRateForEventAsync(connection, Guid.Parse(request.EventsId), ct);
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
        await taxRates.EnsureRateForEventAsync(connection, Guid.Parse(request.EventsId), ct);
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

    public override async Task<CreateBookingResponse> CreateMultiBooking(CreateMultiBookingRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        if (request.Lines.Count == 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Cart is empty"));
        }

        var lines = request.Lines.Select(l => new
        {
            kind = l.Kind,
            ref_id = l.RefId,
            seats = l.Seats
        });
        var linesJson = System.Text.Json.JsonSerializer.Serialize(lines);

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await taxRates.EnsureRateForEventAsync(connection, Guid.Parse(request.EventsId), ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT bookings_id, booking_number FROM sp_create_multi_booking(@u, @ev, @lines::jsonb)", connection);
        cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("lines", linesJson);
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            return new CreateBookingResponse { BookingsId = reader.GetGuid(0).ToString(), BookingNumber = reader.GetString(1) };
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }
    }

    public override async Task<CartQuote> QuoteCart(CreateMultiBookingRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var linesJson = System.Text.Json.JsonSerializer.Serialize(
            request.Lines.Select(l => new { kind = l.Kind, ref_id = l.RefId, seats = l.Seats }));

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await taxRates.EnsureRateForEventAsync(connection, Guid.Parse(request.EventsId), ct);
        decimal taxRate;
        await using (var rateCmd = new NpgsqlCommand("SELECT COALESCE(app.event_tax_rate(@ev), 0)", connection))
        {
            rateCmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
            taxRate = await rateCmd.ExecuteScalarAsync(ct) is decimal r ? r : 0m;
        }
        await using var cmd = new NpgsqlCommand("SELECT kind, ref_id, label, seats, base_price_cents, selling_price_cents, discount_cents, applied_price_rules_id, applied_rule_name, platform_fee_cents, gateway_fee_cents, tax_cents, final_price_cents, organizer_net_cents, currency, ach_available, ach_final_cents FROM sp_quote_cart(@ev, @lines::jsonb)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("lines", linesJson);

        var quote = new CartQuote { Currency = "usd" };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var bd = new PriceBreakdown
            {
                BasePriceCents = reader.GetInt32(4),
                SellingPriceCents = reader.GetInt32(5),
                DiscountCents = reader.GetInt32(6),
                AppliedPriceRulesId = reader.IsDBNull(7) ? string.Empty : reader.GetGuid(7).ToString(),
                AppliedRuleName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                PlatformFeeCents = reader.GetInt32(9),
                GatewayFeeCents = reader.GetInt32(10),
                TaxCents = reader.GetInt32(11),
                FinalPriceCents = reader.GetInt32(12),
                OrganizerNetCents = reader.GetInt32(13),
                Currency = reader.IsDBNull(14) ? "usd" : reader.GetString(14)
            };
            bd.SubtotalCents = bd.SellingPriceCents;
            bd.FeeCents = bd.PlatformFeeCents + bd.GatewayFeeCents + bd.TaxCents;
            bd.TotalCents = bd.FinalPriceCents;

            quote.Lines.Add(new CartQuoteLine
            {
                Kind = reader.GetString(0),
                RefId = reader.GetGuid(1).ToString(),
                Label = reader.GetString(2),
                Seats = reader.GetInt32(3),
                Breakdown = bd
            });

            quote.BaseTotalCents += bd.BasePriceCents;
            quote.SubtotalCents += bd.SellingPriceCents;
            quote.PlatformFeeCents += bd.PlatformFeeCents;
            quote.GatewayFeeCents += bd.GatewayFeeCents;
            quote.TaxCents += bd.TaxCents;
            quote.TotalCents += bd.FinalPriceCents;
            quote.OrganizerNetCents += bd.OrganizerNetCents;
            quote.Currency = bd.Currency;

            quote.AchAvailable = !reader.IsDBNull(15) && reader.GetBoolean(15);
            quote.AchTotalCents += reader.GetInt32(16);
        }
        quote.DiscountCents = quote.BaseTotalCents - quote.SubtotalCents;
        var taxableCents = quote.SubtotalCents + quote.PlatformFeeCents + quote.GatewayFeeCents;
        quote.TaxCents = taxableCents > 0 && taxRate > 0
            ? (int)Math.Round(taxableCents * taxRate, MidpointRounding.AwayFromZero)
            : 0;
        quote.TotalCents = taxableCents + quote.TaxCents;
        quote.FeeCents = quote.PlatformFeeCents + quote.GatewayFeeCents + quote.TaxCents;
        quote.AchSavingsCents = quote.AchAvailable ? Math.Max(quote.TotalCents - quote.AchTotalCents, 0) : 0;
        if (!quote.AchAvailable)
        {
            quote.AchTotalCents = 0;
        }
        quote.HoldSeconds = await settings.GetIntAsync("booking_hold_seconds", 600, ct);
        return quote;
    }

    private static RpcException MapPostgres(PostgresException ex) => ex.SqlState switch
    {
        "P0002" => new RpcException(new Status(StatusCode.NotFound, ex.MessageText)),
        "42501" => new RpcException(new Status(StatusCode.PermissionDenied, ex.MessageText)),
        "22023" => new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText)),
        "23514" => new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText)),
        _ => new RpcException(new Status(StatusCode.Internal, ex.MessageText))
    };

    public override async Task<AckResponse> ConfirmBooking(ConfirmBookingRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var bookingId = Guid.Parse(request.BookingsId);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        
        bool confirmed;
        await using (var cmd = new NpgsqlCommand("SELECT sp_confirm_booking(@id, @qr)", connection))
        {
            cmd.Parameters.AddWithValue("id", bookingId);
            cmd.Parameters.AddWithValue("qr", request.QrToken);
            confirmed = await cmd.ExecuteScalarAsync(ct) is true;
        }

        if (confirmed)
        {
            await BookingEmailSender.SendBookingConfirmationEmailAsync(
                connection, bookingId, email, templates, settings, logger, ct);
        }

        return new AckResponse { Success = true, Message = "Booking confirmed" };
    }

    public override async Task<AckResponse> CancelBooking(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var bookingId = Guid.Parse(request.Value);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        string? intentId = null;
        await using (var look = new NpgsqlCommand(
            "SELECT payment_intent_id FROM vw_stripe_transactions WHERE bookings_id = @b AND status NOT IN ('Succeeded','Refunded')", connection))
        {
            look.Parameters.AddWithValue("b", bookingId);
            intentId = await look.ExecuteScalarAsync(ct) as string;
        }
        if (stripe.Configured && !string.IsNullOrEmpty(intentId))
        {
            await stripe.CancelPaymentIntentAsync(intentId, ct);
        }

        await using (var cmd = new NpgsqlCommand("SELECT sp_cancel_booking(@id)", connection))
        {
            cmd.Parameters.AddWithValue("id", bookingId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return new AckResponse { Success = true, Message = "Booking cancelled" };
    }

    public override Task<AckResponse> RefundBooking(UuidValue request, ServerCallContext context)
        => throw new RpcException(new Status(StatusCode.FailedPrecondition, "All ticket sales are final; refunds are not available."));

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
        var bookingId = Guid.Parse(request.Value);
        Booking booking;
        await using (var cmd = new NpgsqlCommand(BookingSelect + " WHERE b.bookings_id = @id", connection))
        {
            cmd.Parameters.AddWithValue("id", bookingId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
            }
            booking = MapBooking(reader);
        }

        await using (var lc = new NpgsqlCommand(
            "SELECT booking_lines_id, kind, label, event_ticket_types_id, tables_id, "
            + "seats, subtotal_cents, fee_cents, total_cents, "
            + "base_price_cents, selling_price_cents, discount_cents, "
            + "applied_rule_name, platform_fee_cents, gateway_fee_cents, "
            + "0 as tax_cents, final_price_cents, currency "
            + "FROM vw_booking_lines_detail WHERE bookings_id = @id ORDER BY created_at", connection))
        {
            lc.Parameters.AddWithValue("id", bookingId);
            await using var lr = await lc.ExecuteReaderAsync(ct);
            while (await lr.ReadAsync(ct))
            {
                booking.Lines.Add(new BookingLine
                {
                    BookingLinesId = lr.GetGuid(0).ToString(),
                    Kind = lr.GetString(1),
                    Label = lr.GetString(2),
                    EventTicketTypesId = lr.IsDBNull(3) ? string.Empty : lr.GetGuid(3).ToString(),
                    TablesId = lr.IsDBNull(4) ? string.Empty : lr.GetGuid(4).ToString(),
                    Seats = lr.GetInt32(5),
                    SubtotalCents = lr.GetInt32(6),
                    FeeCents = lr.GetInt32(7),
                    TotalCents = lr.GetInt32(8),
                    BasePriceCents = lr.GetInt32(9),
                    SellingPriceCents = lr.GetInt32(10),
                    DiscountCents = lr.GetInt32(11),
                    AppliedRuleName = lr.GetString(12),
                    PlatformFeeCents = lr.GetInt32(13),
                    GatewayFeeCents = lr.GetInt32(14),
                    TaxCents = lr.GetInt32(15),
                    FinalPriceCents = lr.GetInt32(16),
                    Currency = lr.GetString(17)
                });
            }
        }
        return booking;
    }

    public override async Task<ListBookingsResponse> ListBookings(ListBookingsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var page = request.Page ?? new PageRequest();
        var response = new ListBookingsResponse { Meta = new PageMeta { Offset = page.Offset, Limit = page.Limit } };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            BookingSelect + " WHERE (@ev = '00000000-0000-0000-0000-000000000000' OR b.events_id = @ev) "
            + "AND b.status = 'Paid' "
            + "AND (@q = '' OR b.booking_number ILIKE @q OR b.event_title ILIKE @q OR b.guest_search ILIKE @q) "
            + "ORDER BY b.created_at DESC LIMIT @lim OFFSET @off", connection);
        cmd.Parameters.AddWithValue("ev", string.IsNullOrEmpty(request.EventsId) ? Guid.Empty : Guid.Parse(request.EventsId));
        var search = page.Search ?? string.Empty;
        cmd.Parameters.AddWithValue("q", search.Length == 0 ? string.Empty : "%" + search + "%");
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
        "SELECT b.bookings_id, b.booking_number, b.status, b.users_id, b.events_id, b.subtotal_cents, b.fee_cents, b.total_cents, "
        + "COALESCE(b.seats_reserved, 0), COALESCE(b.event_title, ''), COALESCE(b.event_slug, ''), b.event_start_date, "
        + "b.tickets_total, b.tickets_claimed, b.payment_intent_id, "
        + "b.fees_included, COALESCE(b.venue_name, ''), b.venue_address, b.venue_city, b.venue_state, b.venue_zip_code, b.paid_at, "
        + "b.tax_cents, b.service_fee_cents, b.payment_method_type, b.payment_method_last4 "
        + "FROM vw_bookings b";

    private static Booking MapBooking(NpgsqlDataReader r) => new()
    {
        BookingsId = r.GetGuid(0).ToString(),
        BookingNumber = r.GetString(1),
        Status = r.GetString(2),
        EventsId = r.IsDBNull(4) ? string.Empty : r.GetGuid(4).ToString(),
        SubtotalCents = r.GetInt32(5),
        FeeCents = r.GetInt32(6),
        TotalCents = r.GetInt32(7),
        SeatsReserved = r.GetInt32(8),
        EventTitle = r.GetString(9),
        EventSlug = r.GetString(10),
        EventStartDate = r.IsDBNull(11) ? 0 : new DateTimeOffset(r.GetDateTime(11), TimeSpan.Zero).ToUnixTimeSeconds(),
        TicketsTotal = r.GetInt32(12),
        TicketsClaimed = r.GetInt32(13),
        PaymentTransactionId = r.IsDBNull(14) ? string.Empty : r.GetString(14),
        FeesIncluded = !r.IsDBNull(15) && r.GetBoolean(15),
        VenueName = r.GetString(16),
        VenueAddress = ComposeAddress(r.GetString(17), r.GetString(18), r.GetString(19), r.GetString(20)),
        VenueCity = r.GetString(18),
        VenueState = r.GetString(19),
        VenueZip = r.GetString(20),
        PaidAt = r.IsDBNull(21) ? 0 : new DateTimeOffset(r.GetDateTime(21), TimeSpan.Zero).ToUnixTimeSeconds(),
        TaxCents = r.GetInt32(22),
        ServiceFeeCents = r.GetInt32(23),
        PaymentMethodType = r.IsDBNull(24) ? string.Empty : r.GetString(24),
        PaymentMethodLast4 = r.IsDBNull(25) ? string.Empty : r.GetString(25)
    };

    private static string ComposeAddress(string line1, string city, string state, string zip)
    {
        var region = string.Join(' ', new[] { state, zip }).Trim();
        var parts = new List<string>();
        if (line1.Length > 0) parts.Add(line1);
        if (city.Length > 0) parts.Add(city);
        if (region.Length > 0) parts.Add(region);
        return string.Join(", ", parts);
    }

    private void RequireUser()
    {
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
    }
}
