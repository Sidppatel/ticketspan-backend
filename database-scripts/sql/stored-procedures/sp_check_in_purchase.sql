CREATE OR REPLACE FUNCTION sp_check_in_booking(p_qr_token text)
RETURNS TABLE(
    success boolean,
    message text,
    booking_number text,
    guest_name text,
    event_title text,
    status_str text,
    checked_in_at timestamptz
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_booking_id uuid;
    v_booking_number text;
    v_booking_status text;
    v_updated_at timestamptz;
    v_event_title text;
    v_user_name text;
BEGIN
    SELECT p.bookings_id, p.booking_number, p.status, p.updated_at,
           e.title, u.first_name || ' ' || u.last_name
      INTO v_booking_id, v_booking_number, v_booking_status, v_updated_at,
           v_event_title, v_user_name
    FROM bookings p
    JOIN events e ON e.events_id = p.events_id
    JOIN users u ON u.users_id = p.users_id
    WHERE p.qr_token = p_qr_token
    FOR UPDATE OF p;

    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF v_booking_status = 'CheckedIn' THEN
        RETURN QUERY SELECT
            false, 'Already checked in'::text,
            v_booking_number, v_user_name, v_event_title,
            'CheckedIn'::text, v_updated_at;
        RETURN;
    END IF;

    IF v_booking_status <> 'Paid' THEN
        RETURN QUERY SELECT
            false,
            ('Booking is ' || v_booking_status || ' — cannot check in')::text,
            v_booking_number, v_user_name, v_event_title,
            v_booking_status::text, NULL::timestamptz;
        RETURN;
    END IF;

    UPDATE bookings
       SET status = 'CheckedIn', updated_at = now()
     WHERE bookings_id = v_booking_id;

    RETURN QUERY SELECT
        true, 'Check-in successful'::text,
        v_booking_number, v_user_name, v_event_title,
        'CheckedIn'::text, now();
END;
$$;
