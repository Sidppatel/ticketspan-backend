CREATE OR REPLACE FUNCTION sp_deactivate_table_template(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE table_templates SET is_active = false, updated_at = now()
    WHERE table_templates_id = p_id;
END; $$;