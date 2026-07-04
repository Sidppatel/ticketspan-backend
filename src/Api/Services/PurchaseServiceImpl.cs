using Grpc.Core;
using Npgsql;
using Stripe;
using Svyne.Api.Data;
using Svyne.Api.Payments;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Booking;
using Svyne.Protos.Pricing;

using Svyne.Api.Email;

namespace Svyne.Api.Services;

public sealed class BookingServiceImpl : BookingService.BookingServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly StripeService stripe;
    private readonly IEmailService email;
    private readonly EmailTemplateRenderer templates;
    private readonly AppSettingsProvider settings;
    private readonly ILogger<BookingServiceImpl> logger;

    public BookingServiceImpl(
        Db db,
        TenantContext tenantContext,
        StripeService stripe,
        IEmailService email,
        EmailTemplateRenderer templates,
        AppSettingsProvider settings,
        ILogger<BookingServiceImpl> logger)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.stripe = stripe;
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
            "SELECT tt.event_ticket_types_id, tt.label, tt.price_cents, COALESCE(tt.platform_fee_cents, 0), "
            + "COALESCE(tt.max_quantity, 0), COALESCE(tt.description, ''), tt.fee_formulas_id, COALESCE(tt.capacity, 0), "
            + "COALESCE(bp.selling_price_cents, tt.price_cents), COALESCE(vs.sold_count, 0) "
            + "FROM event_ticket_types tt "
            + "LEFT JOIN vw_event_ticket_types_summary vs ON vs.event_ticket_types_id = tt.event_ticket_types_id "
            + "LEFT JOIN prices p ON p.events_id = tt.events_id AND p.pricing_type = 'TicketTier' "
            + "AND lower(p.name) = lower(tt.label) AND p.is_active "
            + "LEFT JOIN LATERAL sp_calculate_price(p.prices_id, 1, now(), -1) bp ON p.prices_id IS NOT NULL "
            + "WHERE tt.events_id = @ev AND tt.is_active = true ORDER BY tt.sort_order, tt.label", connection);
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
                SoldCount = reader.GetInt32(9)
            });
        }
        return response;
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

    public override async Task<CreateBookingResponse> CreateMultiBooking(CreateMultiBookingRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        if (request.Lines.Count == 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Cart is empty"));
        }

        // Build the lines payload the SP consumes. Pricing is resolved server-side,
        // so only kind/ref/seats are passed through.
        var lines = request.Lines.Select(l => new
        {
            kind = l.Kind,
            ref_id = l.RefId,
            seats = l.Seats
        });
        var linesJson = System.Text.Json.JsonSerializer.Serialize(lines);

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
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
        await using var cmd = new NpgsqlCommand("SELECT * FROM sp_quote_cart(@ev, @lines::jsonb)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("lines", linesJson);

        var quote = new CartQuote { Currency = "usd" };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            // Columns: kind, ref_id, label, seats, then the 11 breakdown columns.
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
        quote.FeeCents = quote.PlatformFeeCents + quote.GatewayFeeCents + quote.TaxCents;
        quote.AchSavingsCents = quote.AchAvailable ? Math.Max(quote.TotalCents - quote.AchTotalCents, 0) : 0;
        if (!quote.AchAvailable)
        {
            quote.AchTotalCents = 0;
        }
        quote.HoldSeconds = await settings.GetIntAsync("booking_hold_seconds", 600, ct);
        return quote;
    }

    public override async Task<PaymentIntentResponse> CreatePaymentIntent(PaymentIntentRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        if (!stripe.Configured)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Payments are not configured"));
        }
        if (!Guid.TryParse(request.BookingsId, out var bookingId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid booking id"));
        }

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        long subtotal, fee, total;
        string currency, status;
        string? connectedAccount, existingIntent;
        DateTime? holdExpiresAt;
        bool achAllowed;
        try
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT status, subtotal_cents, fee_cents, total_cents, currency, connected_account_id, "
                + "charges_enabled, existing_intent_id, hold_expires_at, ach_allowed "
                + "FROM sp_get_booking_for_payment(@b, @u)", connection);
            cmd.Parameters.AddWithValue("b", bookingId);
            cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
            }
            status = reader.GetString(0);
            subtotal = reader.GetInt32(1);
            fee = reader.GetInt32(2);
            total = reader.GetInt32(3);
            currency = reader.GetString(4);
            connectedAccount = reader.IsDBNull(5) ? null : reader.GetString(5);
            var chargesEnabled = !reader.IsDBNull(6) && reader.GetBoolean(6);
            existingIntent = reader.IsDBNull(7) ? null : reader.GetString(7);
            holdExpiresAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8);
            achAllowed = !reader.IsDBNull(9) && reader.GetBoolean(9);

            if (string.IsNullOrEmpty(connectedAccount) || !chargesEnabled)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    "Seller is not yet able to accept payments"));
            }
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }

        // Separate ACH checkout: re-price the booking to the ACH fee up front so the
        // bank-only intent is created at the discounted total.
        var preferAch = request.PreferredMethod == "ach" && achAllowed;
        if (preferAch)
        {
            try
            {
                await using var rp = new NpgsqlCommand(
                    "SELECT total_cents, fee_cents FROM sp_reprice_booking_for_method(@b, @u, 'ach')", connection);
                rp.Parameters.AddWithValue("b", bookingId);
                rp.Parameters.AddWithValue("u", tenantContext.UsersId!);
                await using var rr = await rp.ExecuteReaderAsync(ct);
                if (await rr.ReadAsync(ct))
                {
                    total = rr.GetInt32(0);
                    fee = rr.GetInt32(1);
                }
            }
            catch (PostgresException ex)
            {
                throw MapPostgres(ex);
            }
        }

        Stripe.PaymentIntent intent;
        try
        {
            // Resume: reuse an already-committed intent (succeeded/processing) so we never
            // double-charge. A merely-payable existing intent isn't reused on the ACH path,
            // because it may be a card intent — create a fresh bank-only one instead.
            if (!string.IsNullOrEmpty(existingIntent))
            {
                var existing = await stripe.GetPaymentIntentAsync(existingIntent, ct);
                if (existing.Status == "succeeded" || existing.Status == "processing")
                {
                    intent = existing;
                }
                else if (!preferAch && StripeService.IsPayable(existing.Status))
                {
                    intent = existing;
                }
                else
                {
                    intent = await stripe.CreateDestinationPaymentIntentAsync(total, fee, currency, connectedAccount!, bookingId, achAllowed, preferAch, ct);
                }
            }
            else
            {
                intent = await stripe.CreateDestinationPaymentIntentAsync(total, fee, currency, connectedAccount!, bookingId, achAllowed, preferAch, ct);
            }
        }
        catch (StripeException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, $"Stripe payment setup failed: {ex.StripeError?.Message ?? ex.Message}"));
        }

        // transfer_amount = what the seller receives = ticket subtotal.
        await using (var save = new NpgsqlCommand(
            "SELECT sp_upsert_stripe_intent(@b, @intent, @amount, @transfer, @cur)", connection))
        {
            save.Parameters.AddWithValue("b", bookingId);
            save.Parameters.AddWithValue("intent", intent.Id);
            save.Parameters.AddWithValue("amount", (int)total);
            save.Parameters.AddWithValue("transfer", (int)subtotal);
            save.Parameters.AddWithValue("cur", currency);
            await save.ExecuteScalarAsync(ct);
        }

        return new PaymentIntentResponse
        {
            ClientSecret = intent.ClientSecret,
            PublishableKey = stripe.PublishableKey,
            PaymentIntentId = intent.Id,
            Status = intent.Status,
            AmountCents = total,
            HoldExpiresAt = holdExpiresAt is { } h ? new DateTimeOffset(h, TimeSpan.Zero).ToUnixTimeSeconds() : 0,
            AchAllowed = achAllowed
        };
    }

    public override async Task<UpdatePaymentMethodResponse> UpdatePaymentIntentForMethod(
        UpdatePaymentMethodRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        if (!stripe.Configured)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Payments are not configured"));
        }
        if (!Guid.TryParse(request.BookingsId, out var bookingId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid booking id"));
        }
        var method = request.Method == "ach" ? "ach" : "card";

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        int total, fee, baseline;
        string? intentId;
        try
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT total_cents, fee_cents, baseline_total_cents "
                + "FROM sp_reprice_booking_for_method(@b, @u, @m)", connection);
            cmd.Parameters.AddWithValue("b", bookingId);
            cmd.Parameters.AddWithValue("u", tenantContext.UsersId!);
            cmd.Parameters.AddWithValue("m", method);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
            }
            total = reader.GetInt32(0);
            fee = reader.GetInt32(1);
            baseline = reader.GetInt32(2);
        }
        catch (PostgresException ex)
        {
            throw MapPostgres(ex);
        }

        await using (var look = new NpgsqlCommand(
            "SELECT payment_intent_id FROM stripe_transactions WHERE bookings_id = @b "
            + "AND status NOT IN ('Succeeded','Refunded')", connection))
        {
            look.Parameters.AddWithValue("b", bookingId);
            intentId = await look.ExecuteScalarAsync(ct) as string;
        }
        if (string.IsNullOrEmpty(intentId))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "No active payment to update"));
        }

        try
        {
            await stripe.UpdatePaymentIntentAmountAsync(intentId, total, fee, ct);
        }
        catch (StripeException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                $"Stripe update failed: {ex.StripeError?.Message ?? ex.Message}"));
        }

        await using (var save = new NpgsqlCommand(
            "SELECT sp_upsert_stripe_intent(@b, @intent, @amount, @transfer, @cur)", connection))
        {
            save.Parameters.AddWithValue("b", bookingId);
            save.Parameters.AddWithValue("intent", intentId);
            save.Parameters.AddWithValue("amount", total);
            save.Parameters.AddWithValue("transfer", total - fee);
            save.Parameters.AddWithValue("cur", "usd");
            await save.ExecuteScalarAsync(ct);
        }

        return new UpdatePaymentMethodResponse
        {
            TotalCents = total,
            SavingsCents = baseline - total
        };
    }

    public override async Task<PaymentStatusResponse> GetPaymentStatus(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (!Guid.TryParse(request.Value, out var bookingId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid booking id"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT b.status, COALESCE(st.status, '') FROM bookings b "
            + "LEFT JOIN stripe_transactions st ON st.bookings_id = b.bookings_id "
            + "WHERE b.bookings_id = @b", connection);
        cmd.Parameters.AddWithValue("b", bookingId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
        }
        return new PaymentStatusResponse
        {
            BookingStatus = reader.GetString(0),
            PaymentStatus = reader.GetString(1)
        };
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
        
        await using (var cmd = new NpgsqlCommand("SELECT sp_confirm_booking(@id, @qr)", connection))
        {
            cmd.Parameters.AddWithValue("id", bookingId);
            cmd.Parameters.AddWithValue("qr", request.QrToken);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Send booking confirmation email
        await BookingEmailSender.SendBookingConfirmationEmailAsync(
            connection, bookingId, email, templates, settings, logger, ct);

        return new AckResponse { Success = true, Message = "Booking confirmed" };
    }

    public override async Task<AckResponse> CancelBooking(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var bookingId = Guid.Parse(request.Value);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        // Cancel the in-flight PaymentIntent (if any) so the buyer is not charged.
        string? intentId = null;
        await using (var look = new NpgsqlCommand(
            "SELECT payment_intent_id FROM stripe_transactions WHERE bookings_id = @b AND status NOT IN ('Succeeded','Refunded')", connection))
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
        // Policy: all ticket sales are final. Refunds are never initiated in-app.
        // sp_refund_booking stays for the Stripe webhook path (dashboard refunds,
        // dispute reversals) so the DB mirrors whatever Stripe actually did.
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

        // Cart lines (empty for legacy single-line bookings). Label resolves from
        // the linked tier / table.
        await using (var lc = new NpgsqlCommand(
            "SELECT bl.booking_lines_id, bl.kind, "
            + "COALESCE(ett.label, t.label, '') AS label, bl.event_ticket_types_id, bl.tables_id, "
            + "bl.seats, bl.subtotal_cents, bl.fee_cents, bl.total_cents, "
            + "bl.base_price_cents, bl.selling_price_cents, bl.discount_cents, "
            + "COALESCE(bl.applied_rule_name, ''), bl.platform_fee_cents, bl.gateway_fee_cents, "
            + "0 as tax_cents, bl.final_price_cents, bl.currency "
            + "FROM booking_lines bl "
            + "LEFT JOIN event_ticket_types ett ON ett.event_ticket_types_id = bl.event_ticket_types_id "
            + "LEFT JOIN tables t ON t.tables_id = bl.tables_id "
            + "WHERE bl.bookings_id = @id ORDER BY bl.created_at", connection))
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
            + "AND (@q = '' OR b.booking_number ILIKE @q OR b.event_title ILIKE @q OR EXISTS ("
            + "SELECT 1 FROM booking_lines bl LEFT JOIN users gu ON gu.users_id = bl.guest_users_id "
            + "WHERE bl.bookings_id = b.bookings_id AND (bl.ticket_code ILIKE @q OR gu.email ILIKE @q "
            + "OR gu.first_name ILIKE @q OR gu.last_name ILIKE @q))) "
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
        + "(SELECT COUNT(*) FROM booking_lines bl WHERE bl.bookings_id = b.bookings_id AND bl.kind = 'Ticket')::int, "
        + "(SELECT COUNT(*) FROM booking_lines bl WHERE bl.bookings_id = b.bookings_id AND bl.kind = 'Ticket' AND bl.status IN ('Claimed', 'CheckedIn'))::int, "
        + "(SELECT payment_intent_id FROM stripe_transactions st WHERE st.bookings_id = b.bookings_id LIMIT 1), "
        + "b.fees_included, COALESCE(b.venue_name, ''), b.venue_address, b.venue_city, b.venue_state, b.venue_zip_code, b.paid_at "
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
        PaidAt = r.IsDBNull(21) ? 0 : new DateTimeOffset(r.GetDateTime(21), TimeSpan.Zero).ToUnixTimeSeconds()
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
