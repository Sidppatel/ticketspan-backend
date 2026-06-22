CREATE OR REPLACE FUNCTION sp_get_event_by_id_for_layout(p_id uuid)
RETURNS SETOF events
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM events WHERE events_id = p_id;
$$;