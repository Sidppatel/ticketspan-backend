DROP FUNCTION IF EXISTS sp_create_booking(uuid, uuid, uuid, int, uuid, int, int, int, text, text);

CREATE OR REPLACE FUNCTION sp_create_booking(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_seats int,
    p_event_ticket_type_id uuid,
    p_subtotal_cents int, p_fee_cents int, p_total_cents int,
    p_status text DEFAULT 'Pending'
) RETURNS TABLE(bookings_id uuid, booking_number text) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid; v_tenant uuid; v_number text; v_attempt int := 0;
BEGIN
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_event_id;

    -- Booking number BK-* must be unique per (event, user); retry on collision.
    LOOP
        v_attempt := v_attempt + 1;
        v_number := 'BK-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 10));
        BEGIN
            INSERT INTO bookings (tenants_id, booking_number, status, users_id, events_id, tables_id,
                seats_reserved, event_ticket_types_id, subtotal_cents, fee_cents, total_cents,
                created_at, updated_at)
            VALUES (v_tenant, v_number, p_status, p_user_id, p_event_id, p_table_id,
                p_seats, p_event_ticket_type_id, p_subtotal_cents, p_fee_cents, p_total_cents,
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
    END IF;

    bookings_id := v_id;
    booking_number := v_number;
    RETURN NEXT;
END; $$;
