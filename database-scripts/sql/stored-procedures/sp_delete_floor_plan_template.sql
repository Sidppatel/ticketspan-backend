-- Soft delete: deactivate the template (child rows retained).
CREATE OR REPLACE FUNCTION sp_delete_floor_plan_template(p_template_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE floor_plan_templates SET is_active = false, updated_at = now()
    WHERE floor_plan_templates_id = p_template_id;
END; $$;
