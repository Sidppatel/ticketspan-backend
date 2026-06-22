CREATE OR REPLACE FUNCTION sp_get_table_template_by_id(p_id uuid)
RETURNS SETOF table_templates
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM table_templates WHERE table_templates_id = p_id;
$$;