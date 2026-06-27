CREATE OR REPLACE FUNCTION sp_confirm_booking(p_booking_id uuid, p_qr_token text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_seats int; v_seat int; v_tenant uuid; v_event uuid; v_code text; v_attempt int;
BEGIN
    UPDATE bookings SET status = 'Paid', qr_token = p_qr_token,
        hold_expires_at = NULL, updated_at = now()
    -- Accept Pending or a just-Expired hold: if the payment actually succeeded
    -- we honor the seat even if the sweeper raced ahead and expired the hold.
    WHERE bookings_id = p_booking_id AND status IN ('Pending', 'Expired')
    RETURNING seats_reserved, tenants_id, events_id INTO v_seats, v_tenant, v_event;

    IF NOT FOUND THEN
        -- Already Paid (idempotent webhook retry) or Cancelled/Refunded — no-op.
        RETURN;
    END IF;

    UPDATE tables SET status = 'Booked', locked_by_users_id = NULL,
        lock_expires_at = NULL, updated_at = now()
    WHERE tables_id IN (SELECT tables_id FROM booking_tables WHERE bookings_id = p_booking_id)
      AND status IN ('Locked', 'Available');

    -- Tickets to issue: a single-line booking carries seats_reserved; a multi-line
    -- cart booking carries them on its lines (seats_reserved is NULL there).
    v_seats := COALESCE(v_seats,
        (SELECT SUM(seats)::int FROM booking_lines WHERE bookings_id = p_booking_id), 1);
    FOR v_seat IN 1..v_seats LOOP
        -- Ticket number TK-* must be unique within the event; retry on collision.
        v_attempt := 0;
        LOOP
            v_attempt := v_attempt + 1;
            v_code := 'TK-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8));
            BEGIN
                INSERT INTO tickets (tenants_id, bookings_id, events_id, ticket_code, qr_token,
                    seat_number, status, created_at, updated_at)
                VALUES (v_tenant, p_booking_id, v_event, v_code,
                    encode(gen_random_bytes(32), 'hex'),
                    v_seat, 'Unassigned', now(), now());
                EXIT;
            EXCEPTION WHEN unique_violation THEN
                IF v_attempt >= 5 THEN RAISE; END IF;
            END;
        END LOOP;
    END LOOP;
END; $$;
