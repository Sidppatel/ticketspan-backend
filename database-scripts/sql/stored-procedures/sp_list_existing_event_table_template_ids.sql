CREATE OR REPLACE FUNCTION sp_list_existing_event_table_template_ids(p_event_id uuid)
RETURNS TABLE(table_templates_id uuid)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT table_templates_id FROM event_tables
    WHERE events_id = p_event_id AND table_templates_id IS NOT NULL;
$$;