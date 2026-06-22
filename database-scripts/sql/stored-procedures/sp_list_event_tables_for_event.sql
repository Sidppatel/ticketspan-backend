CREATE OR REPLACE FUNCTION sp_list_event_tables_for_event(p_event_id uuid)
RETURNS SETOF event_tables
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM event_tables WHERE events_id = p_event_id ORDER BY label;
$$;