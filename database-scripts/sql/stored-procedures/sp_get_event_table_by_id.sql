CREATE OR REPLACE FUNCTION sp_get_event_table_by_id(p_id uuid)
RETURNS SETOF event_tables
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM event_tables WHERE event_tables_id = p_id;
$$;