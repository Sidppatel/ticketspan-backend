DROP FUNCTION IF EXISTS sp_list_floor_plan_templates();

CREATE OR REPLACE FUNCTION sp_list_floor_plan_templates()
RETURNS TABLE(floor_plan_templates_id uuid, name text,
    table_count int, object_count int, is_active bool)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT f.floor_plan_templates_id, f.name,
        (SELECT count(*)::int FROM floor_plan_template_tables ft
            WHERE ft.floor_plan_templates_id = f.floor_plan_templates_id),
        (SELECT count(*)::int FROM floor_plan_template_objects fo
            WHERE fo.floor_plan_templates_id = f.floor_plan_templates_id),
        f.is_active
    FROM floor_plan_templates f
    WHERE f.is_active = true
    ORDER BY f.name;
$$;
