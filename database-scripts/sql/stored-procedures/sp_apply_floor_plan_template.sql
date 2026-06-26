-- Instantiates a floor-plan template into an event: sets the grid, recreates the
-- table types (event_tables, matched by label), the placed tables, and the layout
-- objects. Skips tables that collide with an existing label/position.
CREATE OR REPLACE FUNCTION sp_apply_floor_plan_template(p_template_id uuid, p_event_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_tenant uuid; v_rows int; v_cols int; r record; v_et uuid; v_formula uuid;
BEGIN
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_event_id;
    v_formula := app.resolve_fee_formula(NULL, v_tenant);

    SELECT grid_rows, grid_cols INTO v_rows, v_cols
      FROM floor_plan_templates WHERE floor_plan_templates_id = p_template_id;
    UPDATE events SET grid_rows = v_rows, grid_cols = v_cols, updated_at = now()
    WHERE events_id = p_event_id;

    FOR r IN SELECT * FROM floor_plan_template_tables WHERE floor_plan_templates_id = p_template_id
    LOOP
        SELECT event_tables_id INTO v_et FROM event_tables
         WHERE events_id = p_event_id AND label = r.type_label LIMIT 1;
        IF v_et IS NULL THEN
            INSERT INTO event_tables (tenants_id, events_id, label, capacity, shape, color,
                price_cents, platform_fee_cents, is_active, created_at, updated_at)
            VALUES (v_tenant, p_event_id, r.type_label, r.capacity, r.shape, r.color,
                r.price_cents, app.compute_fee(r.price_cents, v_formula), true, now(), now())
            RETURNING event_tables_id INTO v_et;
        END IF;

        BEGIN
            INSERT INTO tables (tenants_id, events_id, event_tables_id, label, grid_row, grid_col,
                row_span, col_span, is_active, sort_order, status, created_at, updated_at)
            VALUES (v_tenant, p_event_id, v_et, r.label, r.grid_row, r.grid_col,
                r.row_span, r.col_span, true, r.sort_order, 'Available', now(), now());
        EXCEPTION WHEN unique_violation THEN
            -- Label or grid cell already occupied in the target event; skip.
            CONTINUE;
        END;
    END LOOP;

    INSERT INTO layout_objects (tenants_id, events_id, object_type, label, grid_row, grid_col,
        row_span, col_span, color, sort_order, created_at, updated_at)
    SELECT v_tenant, p_event_id, object_type, label, grid_row, grid_col, row_span, col_span,
        color, sort_order, now(), now()
    FROM floor_plan_template_objects WHERE floor_plan_templates_id = p_template_id;
END; $$;
