DROP FUNCTION IF EXISTS sp_lookup_booking_for_checkin(text, uuid);

CREATE OR REPLACE FUNCTION sp_lookup_booking_for_checkin(
    p_code text,
    p_event_id uuid
)
RETURNS TABLE(
    bookings_id uuid
) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT b.bookings_id
    FROM bookings b
    WHERE b.events_id = p_event_id
      AND (b.booking_number = p_code OR b.qr_token = p_code)
    LIMIT 1;
$$;
