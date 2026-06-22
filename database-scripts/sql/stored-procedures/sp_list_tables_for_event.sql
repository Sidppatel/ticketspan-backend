CREATE OR REPLACE FUNCTION sp_list_tables_for_event(p_event_id uuid)
RETURNS SETOF tables
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM tables WHERE events_id = p_event_id ORDER BY sort_order;
$$;