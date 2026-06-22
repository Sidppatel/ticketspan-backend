CREATE OR REPLACE FUNCTION sp_list_active_table_templates()
RETURNS SETOF table_templates
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM table_templates ORDER BY name;
$$;