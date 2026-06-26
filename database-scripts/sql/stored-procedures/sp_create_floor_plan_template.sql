-- Snapshots an event's current floor plan (grid + tables + objects) into a
-- reusable, tenant-scoped template.
CREATE OR REPLACE FUNCTION sp_create_floor_plan_template(p_event_id uuid, p_name text)
RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid; v_tenant uuid; v_rows int; v_cols int;
BEGIN
    SELECT tenants_id, grid_rows, grid_cols INTO v_tenant, v_rows, v_cols
      FROM events WHERE events_id = p_event_id;

    INSERT INTO floor_plan_templates (tenants_id, name, grid_rows, grid_cols,
        is_active, created_at, updated_at)
    VALUES (v_tenant, p_name, COALESCE(v_rows, 0), COALESCE(v_cols, 0), true, now(), now())
    RETURNING floor_plan_templates_id INTO v_id;

    INSERT INTO floor_plan_template_tables (tenants_id, floor_plan_templates_id, label,
        type_label, capacity, shape, color, price_cents, grid_row, grid_col,
        row_span, col_span, sort_order, created_at, updated_at)
    SELECT v_tenant, v_id, t.label, et.label,
        COALESCE(t.capacity_override, et.capacity),
        COALESCE(t.shape_override, et.shape),
        COALESCE(t.color_override, et.color),
        et.price_cents, t.grid_row, t.grid_col, t.row_span, t.col_span, t.sort_order,
        now(), now()
    FROM tables t JOIN event_tables et ON et.event_tables_id = t.event_tables_id
    WHERE t.events_id = p_event_id;

    INSERT INTO floor_plan_template_objects (tenants_id, floor_plan_templates_id,
        object_type, label, grid_row, grid_col, row_span, col_span, color, sort_order,
        created_at, updated_at)
    SELECT v_tenant, v_id, object_type, label, grid_row, grid_col, row_span, col_span,
        color, sort_order, now(), now()
    FROM layout_objects WHERE events_id = p_event_id;

    RETURN v_id;
END; $$;
