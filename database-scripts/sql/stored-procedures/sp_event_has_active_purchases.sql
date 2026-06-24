CREATE OR REPLACE FUNCTION sp_event_has_active_bookings(p_event_id uuid)
RETURNS boolean LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT EXISTS(
        SELECT 1 FROM bookings
        WHERE events_id = p_event_id
          AND status NOT IN ('Cancelled', 'Refunded')
    );
$$;