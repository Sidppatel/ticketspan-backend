DROP FUNCTION IF EXISTS sp_create_booking(uuid, uuid, uuid, int, uuid, int, int, int, text, text);
DROP FUNCTION IF EXISTS sp_create_booking(uuid, uuid, uuid, int, uuid, int, int, int, text);

CREATE OR REPLACE FUNCTION sp_create_booking(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_seats int,
    p_event_ticket_type_id uuid,
    p_subtotal_cents int, p_fee_cents int, p_total_cents int,
    p_status text DEFAULT 'Pending'
) RETURNS TABLE(bookings_id uuid, booking_number text) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid; v_tenant uuid; v_number text; v_attempt int := 0; v_hold int; v_tbl_status text;
        v_unit_price int; v_formula uuid;
        v_subtotal int := p_subtotal_cents; v_fee int := p_fee_cents; v_total int := p_total_cents;
BEGIN
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_event_id;

    -- Server-authoritative pricing: recompute subtotal/fee/total from the
    -- table or ticket type, ignoring client-sent amounts. A table books as a
    -- single unit; a ticket type multiplies by seats.
    IF p_table_id IS NOT NULL THEN
        SELECT et.price_cents, et.fee_formulas_id INTO v_unit_price, v_formula
          FROM tables t JOIN event_tables et ON et.event_tables_id = t.event_tables_id
         WHERE t.tables_id = p_table_id;
        IF v_unit_price IS NOT NULL THEN
            v_subtotal := v_unit_price;
            v_fee := app.compute_fee(v_unit_price, v_formula);
            v_total := v_subtotal + v_fee;
        END IF;
    ELSIF p_event_ticket_type_id IS NOT NULL THEN
        SELECT price_cents, fee_formulas_id INTO v_unit_price, v_formula
          FROM event_ticket_types WHERE event_ticket_types_id = p_event_ticket_type_id;
        IF v_unit_price IS NOT NULL THEN
            v_subtotal := v_unit_price * COALESCE(p_seats, 1);
            v_fee := app.compute_fee(v_unit_price, v_formula) * COALESCE(p_seats, 1);
            v_total := v_subtotal + v_fee;
        END IF;
    END IF;

    SELECT COALESCE((SELECT value::int FROM app_settings WHERE key = 'booking_hold_seconds'), 600)
      INTO v_hold;

    -- Idempotency / resume: reuse this user's live Pending hold for the same
    -- table (double-click, tab switch, mid-payment navigation).
    IF p_status = 'Pending' AND p_table_id IS NOT NULL THEN
        SELECT b.bookings_id, b.booking_number INTO v_id, v_number
          FROM bookings b
          WHERE b.users_id = p_user_id
            AND b.events_id = p_event_id
            AND b.tables_id = p_table_id
            AND b.status = 'Pending'
            AND (b.hold_expires_at IS NULL OR b.hold_expires_at > now())
          ORDER BY b.created_at DESC
          LIMIT 1;

        IF v_id IS NOT NULL THEN
            UPDATE bookings
               SET hold_expires_at = now() + make_interval(secs => v_hold), updated_at = now()
             WHERE bookings.bookings_id = v_id;
            UPDATE tables
               SET lock_expires_at = now() + make_interval(secs => v_hold), updated_at = now()
             WHERE tables_id = p_table_id;
            bookings_id := v_id;
            booking_number := v_number;
            RETURN NEXT;
            RETURN;
        END IF;
    END IF;

    -- Guard: do not let two users hold the same table at once.
    IF p_table_id IS NOT NULL THEN
        SELECT status INTO v_tbl_status FROM tables WHERE tables_id = p_table_id FOR UPDATE;
        IF v_tbl_status = 'Booked' THEN
            RAISE EXCEPTION 'Table already booked' USING ERRCODE = '23514';
        END IF;
        IF v_tbl_status = 'Locked' THEN
            -- Allow only if the existing lock is stale.
            IF EXISTS (SELECT 1 FROM tables
                       WHERE tables_id = p_table_id
                         AND lock_expires_at IS NOT NULL AND lock_expires_at > now()) THEN
                RAISE EXCEPTION 'Table is currently held by another user' USING ERRCODE = '23514';
            END IF;
        END IF;
    END IF;

    -- Booking number BK-* must be unique per (event, user); retry on collision.
    LOOP
        v_attempt := v_attempt + 1;
        v_number := 'BK-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 10));
        BEGIN
            INSERT INTO bookings (tenants_id, booking_number, status, users_id, events_id, tables_id,
                seats_reserved, event_ticket_types_id, subtotal_cents, fee_cents, total_cents,
                hold_expires_at, created_at, updated_at)
            VALUES (v_tenant, v_number, p_status, p_user_id, p_event_id, p_table_id,
                p_seats, p_event_ticket_type_id, v_subtotal, v_fee, v_total,
                CASE WHEN p_status = 'Pending' THEN now() + make_interval(secs => v_hold) ELSE NULL END,
                now(), now())
            RETURNING bookings.bookings_id INTO v_id;
            EXIT;
        EXCEPTION WHEN unique_violation THEN
            IF v_attempt >= 5 THEN RAISE; END IF;
        END;
    END LOOP;

    IF p_table_id IS NOT NULL THEN
        INSERT INTO booking_tables (tenants_id, bookings_id, tables_id)
        VALUES (v_tenant, v_id, p_table_id);

        -- Lock the table for the hold window (only for unpaid holds).
        IF p_status = 'Pending' THEN
            UPDATE tables
               SET status = 'Locked', locked_by_users_id = p_user_id,
                   lock_expires_at = now() + make_interval(secs => v_hold), updated_at = now()
             WHERE tables_id = p_table_id;
        END IF;
    END IF;

    bookings_id := v_id;
    booking_number := v_number;
    RETURN NEXT;
END; $$;
