CREATE OR REPLACE FUNCTION sp_list_active_table_templates_by_ids(p_ids uuid[])
RETURNS SETOF table_templates
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM table_templates WHERE table_templates_id = ANY(p_ids) AND is_active = true;
$$;