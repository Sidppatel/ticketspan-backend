DROP FUNCTION IF EXISTS sp_check_in_booking_by_number(text, uuid, uuid);

CREATE OR REPLACE FUNCTION sp_check_in_booking_by_number(
    p_booking_number text,
    p_event_id uuid,
    p_staff_user_id uuid,
    p_method text DEFAULT 'manual_entry'
)
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
BEGIN
    SELECT b.bookings_id INTO v_booking_id
    FROM bookings b
    WHERE b.booking_number = p_booking_number AND b.events_id = p_event_id;

    IF NOT FOUND THEN
        PERFORM sp_log_checkin_attempt(p_event_id, p_staff_user_id, NULL, NULL, p_method, 'failed', 'booking_not_found');
        RETURN QUERY SELECT false, 'Booking number not found'::text, NULL::text, NULL::text, NULL::text, NULL::text, NULL::timestamptz;
        RETURN;
    END IF;

    RETURN QUERY SELECT * FROM sp_check_in_booking(v_booking_id, p_event_id, p_staff_user_id, p_method);
END;
$$;
