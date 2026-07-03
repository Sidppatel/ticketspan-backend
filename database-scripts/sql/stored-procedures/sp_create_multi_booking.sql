DROP FUNCTION IF EXISTS sp_create_multi_booking(uuid, uuid, jsonb);

-- Cart checkout: create ONE Pending booking that aggregates many lines (any mix
-- of ticket tiers and tables) into a single transaction / Stripe PaymentIntent.
-- Pricing is fully server-authoritative (app.price_breakdown per line); the client
-- never supplies amounts. Each line stores an IMMUTABLE pricing snapshot (base,
-- selling, discount, applied rule, platform/gateway fees, final, currency) so
-- historical bookings never drift when rules or prices change later. Table lines
-- reserve the WHOLE table (seats = capacity) and lock the table row atomically for
-- the hold window. The whole cart succeeds or fails together.
--
-- p_lines: jsonb array of {"kind":"Ticket"|"Table","ref_id":uuid,"seats":int}
--   Ticket -> ref_id = event_ticket_types_id, seats = requested quantity
--   Table  -> ref_id = tables_id,            seats ignored (= table capacity)
CREATE OR REPLACE FUNCTION sp_create_multi_booking(
    p_user_id uuid, p_event_id uuid, p_lines jsonb
) RETURNS TABLE(bookings_id uuid, booking_number text) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_tenant uuid; v_event_type text; v_hold int; v_number text; v_id uuid; v_attempt int := 0;
    v_sub int := 0; v_fee int := 0; v_total int := 0; v_seats_total int := 0;
    v_line jsonb; v_kind text; v_ref uuid; v_seats int;
    v_prices_id uuid;
    v_cap int; v_tbl_status text; v_locked_by uuid; v_lock_exp timestamptz;
    v_tt_max int; v_tt_sold int;
    v_bd record;
    v_resolved jsonb := '[]'::jsonb;
    v_seat_idx int;
    v_global_seat_idx int := 0;
    v_code text;
    v_qr text;
BEGIN
    SELECT event_type, tenants_id INTO v_event_type, v_tenant
      FROM events WHERE events_id = p_event_id FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Event not found' USING ERRCODE = 'P0002';
    END IF;
    PERFORM app.assert_tenant_sellable(v_tenant);

    IF p_lines IS NULL OR jsonb_typeof(p_lines) <> 'array' OR jsonb_array_length(p_lines) = 0 THEN
        RAISE EXCEPTION 'No items in cart' USING ERRCODE = '22023';
    END IF;

    SELECT COALESCE((SELECT value::int FROM app_settings WHERE key = 'booking_hold_seconds'), 600)
      INTO v_hold;

    -- Pass 1: validate + resolve every line, locking each table row. No booking
    -- writes yet, so any failure here aborts the whole cart cleanly.
    FOR v_line IN SELECT * FROM jsonb_array_elements(p_lines) LOOP
        v_kind := v_line->>'kind';
        v_ref := (v_line->>'ref_id')::uuid;
        v_seats := COALESCE((v_line->>'seats')::int, 1);

        IF v_kind = 'Ticket' THEN
            IF v_event_type NOT IN ('Open', 'Both') THEN
                RAISE EXCEPTION 'Event does not sell tickets' USING ERRCODE = '22023';
            END IF;
            v_seats := GREATEST(v_seats, 1);

            SELECT prices_id, max_quantity
              INTO v_prices_id, v_tt_max
              FROM event_ticket_types WHERE event_ticket_types_id = v_ref FOR UPDATE;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'Ticket type not found' USING ERRCODE = 'P0002';
            END IF;
            IF v_prices_id IS NULL THEN
                RAISE EXCEPTION 'Ticket type has no linked price' USING ERRCODE = '22023';
            END IF;

            -- Per-tier cap: live seats already taken + this line.
            IF v_tt_max IS NOT NULL THEN
                v_tt_sold := app.ticket_type_seats_live(v_ref);
                IF v_tt_sold + v_seats > v_tt_max THEN
                    RAISE EXCEPTION 'Not enough availability for ticket type. Available: %, requested: %',
                        v_tt_max - v_tt_sold, v_seats USING ERRCODE = '23514';
                END IF;
            END IF;

            -- Calculate single ticket unit price breakdown
            SELECT * INTO v_bd FROM app.price_breakdown(v_prices_id, now(), 1,
                                     app.remaining_for_price(v_prices_id));

            v_resolved := v_resolved || jsonb_build_object(
                'kind', 'Ticket', 'ett', v_ref::text, 'tbl', NULL,
                'prices_id', v_prices_id::text, 'seats', v_seats,
                'base', v_bd.base_price_cents, 'selling', v_bd.selling_price_cents,
                'discount', v_bd.discount_cents, 'rule_id', v_bd.applied_price_rules_id,
                'rule_name', v_bd.applied_rule_name, 'platform', v_bd.platform_fee_cents,
                'gateway', v_bd.gateway_fee_cents,
                'final', v_bd.final_price_cents, 'currency', v_bd.currency);

            v_sub := v_sub + v_bd.selling_price_cents * v_seats;
            v_fee := v_fee + (v_bd.platform_fee_cents + v_bd.gateway_fee_cents) * v_seats;
            v_total := v_total + v_bd.final_price_cents * v_seats;
            v_seats_total := v_seats_total + v_seats;

        ELSIF v_kind = 'Table' THEN
            IF v_event_type NOT IN ('Table', 'Both') THEN
                RAISE EXCEPTION 'Event does not sell tables' USING ERRCODE = '22023';
            END IF;

            SELECT t.status, t.locked_by_users_id, t.lock_expires_at,
                   COALESCE(t.capacity_override, et.capacity), et.prices_id
              INTO v_tbl_status, v_locked_by, v_lock_exp, v_cap, v_prices_id
              FROM tables t JOIN event_tables et ON et.event_tables_id = t.event_tables_id
             WHERE t.tables_id = v_ref FOR UPDATE OF t;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'Table not found' USING ERRCODE = 'P0002';
            END IF;
            IF v_prices_id IS NULL THEN
                RAISE EXCEPTION 'Table has no linked price' USING ERRCODE = '22023';
            END IF;
            IF v_tbl_status = 'Booked' THEN
                RAISE EXCEPTION 'Table % already booked', v_ref USING ERRCODE = '23514';
            END IF;
            -- A live lock owned by another user blocks; the buyer's own lock is fine.
            IF v_tbl_status = 'Locked' AND v_lock_exp IS NOT NULL AND v_lock_exp > now()
               AND v_locked_by IS DISTINCT FROM p_user_id THEN
                RAISE EXCEPTION 'Table is currently held by another user' USING ERRCODE = '23514';
            END IF;

            v_seats := GREATEST(COALESCE(v_cap, 1), 1);

            -- Calculate total table breakdown
            SELECT * INTO v_bd FROM app.price_breakdown(v_prices_id, now(), v_seats,
                                     app.remaining_for_price(v_prices_id));

            v_resolved := v_resolved || jsonb_build_object(
                'kind', 'Table', 'ett', NULL, 'tbl', v_ref::text,
                'prices_id', v_prices_id::text, 'seats', v_seats,
                'base', v_bd.base_price_cents, 'selling', v_bd.selling_price_cents,
                'discount', v_bd.discount_cents, 'rule_id', v_bd.applied_price_rules_id,
                'rule_name', v_bd.applied_rule_name, 'platform', v_bd.platform_fee_cents,
                'gateway', v_bd.gateway_fee_cents,
                'final', v_bd.final_price_cents, 'currency', v_bd.currency);

            v_sub := v_sub + v_bd.selling_price_cents;
            v_fee := v_fee + v_bd.platform_fee_cents + v_bd.gateway_fee_cents;
            v_total := v_total + v_bd.final_price_cents;
            v_seats_total := v_seats_total + v_seats;
        ELSE
            RAISE EXCEPTION 'Unknown line kind: %', v_kind USING ERRCODE = '22023';
        END IF;
    END LOOP;

    -- Create the aggregate booking. All detail lives on the lines; booking totals
    -- are the cart sum (total = subtotal + fee invariant preserved).
    LOOP
        v_attempt := v_attempt + 1;
        v_number := 'BK-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 10));
        BEGIN
            INSERT INTO bookings (tenants_id, booking_number, status, users_id, events_id,
                subtotal_cents, fee_cents, total_cents, seats_reserved, hold_expires_at, created_at, updated_at)
            VALUES (v_tenant, v_number, 'Pending', p_user_id, p_event_id,
                v_sub, v_fee, v_total, v_seats_total,
                now() + make_interval(secs => v_hold), now(), now())
            RETURNING bookings.bookings_id INTO v_id;
            EXIT;
        EXCEPTION WHEN unique_violation THEN
            IF v_attempt >= 5 THEN RAISE; END IF;
        END;
    END LOOP;

    -- Pass 2: persist lines (with full immutable snapshot) + lock table rows.
    FOR v_line IN SELECT * FROM jsonb_array_elements(v_resolved) LOOP
        v_kind := v_line->>'kind';
        IF v_kind = 'Ticket' THEN
            v_seats := (v_line->>'seats')::int;
            FOR v_seat_idx IN 1..v_seats LOOP
                v_global_seat_idx := v_global_seat_idx + 1;
                v_code := 'TK-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8));
                v_qr := encode(gen_random_bytes(32), 'hex');

                INSERT INTO booking_lines (tenants_id, bookings_id, events_id, kind, event_ticket_types_id,
                    tables_id, prices_id, seats, ticket_code, qr_token, seat_number, status,
                    base_price_cents, selling_price_cents, discount_cents,
                    applied_price_rules_id, applied_rule_name,
                    platform_fee_cents, gateway_fee_cents,
                    subtotal_cents, fee_cents, total_cents, final_price_cents, currency,
                    created_at, updated_at)
                VALUES (v_tenant, v_id, p_event_id, 'Ticket', (v_line->>'ett')::uuid,
                    NULL, (v_line->>'prices_id')::uuid, 1, v_code, v_qr, v_global_seat_idx, 'Unassigned',
                    (v_line->>'base')::int, (v_line->>'selling')::int, (v_line->>'discount')::int,
                    NULLIF(v_line->>'rule_id','')::uuid, NULLIF(v_line->>'rule_name',''),
                    (v_line->>'platform')::int, (v_line->>'gateway')::int,
                    (v_line->>'selling')::int,
                    (v_line->>'platform')::int + (v_line->>'gateway')::int,
                    (v_line->>'final')::int, (v_line->>'final')::int, v_line->>'currency',
                    now(), now());
            END LOOP;
        ELSIF v_kind = 'Table' THEN
            v_code := 'TBL-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8));
            v_qr := encode(gen_random_bytes(32), 'hex');

            INSERT INTO booking_lines (tenants_id, bookings_id, events_id, kind, event_ticket_types_id,
                tables_id, prices_id, seats, ticket_code, qr_token, status,
                base_price_cents, selling_price_cents, discount_cents,
                applied_price_rules_id, applied_rule_name,
                platform_fee_cents, gateway_fee_cents,
                subtotal_cents, fee_cents, total_cents, final_price_cents, currency,
                created_at, updated_at)
            VALUES (v_tenant, v_id, p_event_id, 'Table', NULL,
                (v_line->>'tbl')::uuid, (v_line->>'prices_id')::uuid, (v_line->>'seats')::int, v_code, v_qr, 'Unassigned',
                (v_line->>'base')::int, (v_line->>'selling')::int, (v_line->>'discount')::int,
                NULLIF(v_line->>'rule_id','')::uuid, NULLIF(v_line->>'rule_name',''),
                (v_line->>'platform')::int, (v_line->>'gateway')::int,
                (v_line->>'selling')::int,
                (v_line->>'platform')::int + (v_line->>'gateway')::int,
                (v_line->>'final')::int, (v_line->>'final')::int, v_line->>'currency',
                now(), now());

            UPDATE tables
               SET status = 'Locked', locked_by_users_id = p_user_id,
                   lock_expires_at = now() + make_interval(secs => v_hold), updated_at = now()
             WHERE tables_id = (v_line->>'tbl')::uuid;

            v_seats := (v_line->>'seats')::int;
            FOR v_seat_idx IN 1..v_seats LOOP
                v_global_seat_idx := v_global_seat_idx + 1;
                v_code := 'TBL-TK-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8));
                v_qr := encode(gen_random_bytes(32), 'hex');

                INSERT INTO booking_lines (tenants_id, bookings_id, events_id, kind, event_ticket_types_id,
                    tables_id, prices_id, seats, ticket_code, qr_token, seat_number, status,
                    base_price_cents, selling_price_cents, discount_cents,
                    applied_price_rules_id, applied_rule_name,
                    platform_fee_cents, gateway_fee_cents,
                    subtotal_cents, fee_cents, total_cents, final_price_cents, currency,
                    created_at, updated_at)
                VALUES (v_tenant, v_id, p_event_id, 'Ticket', NULL,
                    (v_line->>'tbl')::uuid, (v_line->>'prices_id')::uuid, 1, v_code, v_qr, v_global_seat_idx, 'Unassigned',
                    0, 0, 0, NULL, NULL, 0, 0, 0, 0, 0, 0, v_line->>'currency',
                    now(), now());
            END LOOP;
        END IF;
    END LOOP;

    bookings_id := v_id;
    booking_number := v_number;
    RETURN NEXT;
END; $$;
