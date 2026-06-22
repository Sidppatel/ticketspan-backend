CREATE OR REPLACE FUNCTION sp_get_table_by_id(p_id uuid)
RETURNS SETOF tables
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM tables WHERE tables_id = p_id;
$$;