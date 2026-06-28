DROP FUNCTION IF EXISTS sp_reserve_open_capacity(uuid, uuid, int, uuid, int, int, int, text);
DROP FUNCTION IF EXISTS sp_reserve_open_capacity(uuid, uuid, int, uuid, int, int, int);

CREATE OR REPLACE FUNCTION sp_reserve_open_capacity(
    p_user_id uuid,
    p_event_id uuid,
    p_seats int,
    p_event_ticket_type_id uuid,
    p_subtotal_cents int,
    p_fee_cents int,
    p_total_cents int
) RETURNS TABLE(bookings_id uuid, booking_number text) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
    v_event_type text;
    v_max_capacity int;
    v_total_reserved int;
    v_tt_max int;
    v_tt_sold int;
    v_tenant uuid;
    v_number text;
    v_attempt int := 0;
    v_hold int;
    v_unit_price int;
    v_formula uuid;
    v_subtotal int := p_subtotal_cents;
    v_fee int := p_fee_cents;
    v_total int := p_total_cents;
BEGIN
    -- Hard hold window (seconds) the Pending booking reserves the seats for.
    SELECT COALESCE((SELECT value::int FROM app_settings WHERE key = 'booking_hold_seconds'), 600)
      INTO v_hold;

    -- Idempotency / resume: if this user already holds a live Pending booking
    -- for the same event + ticket type, return it instead of creating a new one
    -- (handles double-clicks, tab switches, mid-payment navigation).
    SELECT b.bookings_id, b.booking_number INTO v_id, v_number
      FROM bookings b
      WHERE b.users_id = p_user_id
        AND b.events_id = p_event_id
        AND b.status = 'Pending'
        AND b.tables_id IS NULL
        AND b.event_ticket_types_id IS NOT DISTINCT FROM p_event_ticket_type_id
        AND (b.hold_expires_at IS NULL OR b.hold_expires_at > now())
      ORDER BY b.created_at DESC
      LIMIT 1;

    IF v_id IS NOT NULL THEN
        -- Refresh the hold window on resume.
        UPDATE bookings
           SET hold_expires_at = now() + make_interval(secs => v_hold), updated_at = now()
         WHERE bookings.bookings_id = v_id;
        bookings_id := v_id;
        booking_number := v_number;
        RETURN NEXT;
        RETURN;
    END IF;

    SELECT event_type, tenants_id
      INTO v_event_type, v_tenant
      FROM events
      WHERE events_id = p_event_id
      FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Event not found' USING ERRCODE = 'P0002';
    END IF;

    -- Event-level open cap = sum of active ticket-type capacities (replaces the
    -- removed events.max_capacity column). 0 = uncapped.
    SELECT COALESCE(SUM(capacity), 0)
      INTO v_max_capacity
      FROM event_ticket_types
      WHERE events_id = p_event_id AND is_active = true;
    -- Open ticket capacity is sold by Open and Both events; Table-only events have
    -- no open seats.
    IF v_event_type NOT IN ('Open', 'Both') THEN
        RAISE EXCEPTION 'Event does not sell open capacity' USING ERRCODE = '22023';
    END IF;

    -- Live reservations across both booking models (single-line + multi-line cart),
    -- Paid/CheckedIn always counting and Pending only while its hold is alive.
    v_total_reserved := app.event_seats_live(p_event_id);

    -- Event-level cap is optional (esp. for Both events): only enforce when set.
    IF v_max_capacity IS NOT NULL AND v_max_capacity > 0
       AND v_total_reserved + p_seats > v_max_capacity THEN
        RAISE EXCEPTION 'Not enough capacity. Available: %, requested: %',
            v_max_capacity - v_total_reserved, p_seats USING ERRCODE = '23514';
    END IF;

    IF p_event_ticket_type_id IS NOT NULL THEN
        SELECT max_quantity, price_cents, fee_formulas_id
          INTO v_tt_max, v_unit_price, v_formula
          FROM event_ticket_types
          WHERE event_ticket_types_id = p_event_ticket_type_id
          FOR UPDATE;

        -- Server-authoritative pricing: recompute from the ticket type, ignoring
        -- the client-sent amounts (they could be tampered to underpay svyne).
        v_subtotal := v_unit_price * p_seats;
        v_fee := app.compute_fee(v_unit_price, v_formula) * p_seats;
        v_total := v_subtotal + v_fee;

        IF v_tt_max IS NOT NULL THEN
            v_tt_sold := app.ticket_type_seats_live(p_event_ticket_type_id);

            IF v_tt_sold + p_seats > v_tt_max THEN
                RAISE EXCEPTION 'Not enough availability for ticket type. Available: %, requested: %',
                    v_tt_max - v_tt_sold, p_seats USING ERRCODE = '23514';
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
            VALUES (v_tenant, v_number, 'Pending', p_user_id, p_event_id, NULL,
                p_seats, p_event_ticket_type_id, v_subtotal, v_fee, v_total,
                now() + make_interval(secs => v_hold), now(), now())
            RETURNING bookings.bookings_id INTO v_id;
            EXIT;
        EXCEPTION WHEN unique_violation THEN
            IF v_attempt >= 5 THEN RAISE; END IF;
        END;
    END LOOP;

    bookings_id := v_id;
    booking_number := v_number;
    RETURN NEXT;
END; $$;
