-- Old 5-arg signature is replaced by the 6-arg version below (adds p_objects).
DROP FUNCTION IF EXISTS sp_save_event_layout(uuid, int, int, jsonb, uuid[]);

CREATE OR REPLACE FUNCTION sp_save_event_layout(
    p_event_id uuid, p_grid_rows int, p_grid_cols int,
    p_tables jsonb, p_locked_ids uuid[], p_objects jsonb DEFAULT '[]'::jsonb
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_request_ids uuid[];
    v_object_ids uuid[];
    v_table jsonb;
    v_obj jsonb;
    v_id uuid;
    v_tenant uuid;
BEGIN
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_event_id;

    -- 0 means "grid unset" from the client; store NULL to satisfy the
    -- CK_events_GridDimensions check (grid dims must be > 0 or NULL).
    UPDATE events SET grid_rows = NULLIF(p_grid_rows, 0), grid_cols = NULLIF(p_grid_cols, 0),
        updated_at = now()
    WHERE events_id = p_event_id;

    SELECT COALESCE(array_agg(NULLIF(t->>'Id', '')::uuid) FILTER (WHERE NULLIF(t->>'Id', '') IS NOT NULL), '{}')
    INTO v_request_ids
    FROM jsonb_array_elements(p_tables) AS t;

    DELETE FROM tables
    WHERE events_id = p_event_id
      AND tables_id <> ALL(v_request_ids)
      AND tables_id <> ALL(p_locked_ids);

    FOR v_table IN SELECT * FROM jsonb_array_elements(p_tables)
    LOOP
        v_id := NULLIF(v_table->>'Id', '')::uuid;
        IF v_id IS NOT NULL AND v_id = ANY(p_locked_ids) THEN
            CONTINUE;
        END IF;

        IF v_id IS NOT NULL AND EXISTS(SELECT 1 FROM tables WHERE tables_id = v_id) THEN
            UPDATE tables SET
                label = v_table->>'Label',
                grid_row = (v_table->>'GridRow')::int,
                grid_col = (v_table->>'GridCol')::int,
                is_active = (v_table->>'IsActive')::bool,
                sort_order = (v_table->>'SortOrder')::int,
                event_tables_id = (v_table->>'EventTableId')::uuid,
                row_span = COALESCE((v_table->>'RowSpan')::int, 1),
                col_span = COALESCE((v_table->>'ColSpan')::int, 1),
                shape_override = NULLIF(v_table->>'ShapeOverride', ''),
                color_override = NULLIF(v_table->>'ColorOverride', ''),
                capacity_override = NULLIF(v_table->>'CapacityOverride', '')::int,
                updated_at = now()
            WHERE tables_id = v_id;
        ELSE
            INSERT INTO tables (tables_id, tenants_id, events_id, event_tables_id, label,
                grid_row, grid_col, is_active, sort_order, status,
                row_span, col_span, shape_override, color_override, capacity_override,
                created_at, updated_at)
            VALUES (
                COALESCE(v_id, gen_random_uuid()), v_tenant, p_event_id,
                (v_table->>'EventTableId')::uuid,
                v_table->>'Label',
                (v_table->>'GridRow')::int,
                (v_table->>'GridCol')::int,
                (v_table->>'IsActive')::bool,
                (v_table->>'SortOrder')::int,
                'Available',
                COALESCE((v_table->>'RowSpan')::int, 1),
                COALESCE((v_table->>'ColSpan')::int, 1),
                NULLIF(v_table->>'ShapeOverride', ''),
                NULLIF(v_table->>'ColorOverride', ''),
                NULLIF(v_table->>'CapacityOverride', '')::int,
                now(), now()
            );
        END IF;
    END LOOP;

    -- Layout objects (Entry / Exit / Stage): upsert the request set, delete the rest.
    SELECT COALESCE(array_agg(NULLIF(o->>'Id', '')::uuid) FILTER (WHERE NULLIF(o->>'Id', '') IS NOT NULL), '{}')
    INTO v_object_ids
    FROM jsonb_array_elements(p_objects) AS o;

    DELETE FROM layout_objects
    WHERE events_id = p_event_id
      AND layout_objects_id <> ALL(v_object_ids);

    FOR v_obj IN SELECT * FROM jsonb_array_elements(p_objects)
    LOOP
        v_id := NULLIF(v_obj->>'Id', '')::uuid;
        IF v_id IS NOT NULL AND EXISTS(SELECT 1 FROM layout_objects WHERE layout_objects_id = v_id) THEN
            UPDATE layout_objects SET
                object_type = v_obj->>'ObjectType',
                label = NULLIF(v_obj->>'Label', ''),
                grid_row = (v_obj->>'GridRow')::int,
                grid_col = (v_obj->>'GridCol')::int,
                row_span = COALESCE((v_obj->>'RowSpan')::int, 1),
                col_span = COALESCE((v_obj->>'ColSpan')::int, 1),
                color = NULLIF(v_obj->>'Color', ''),
                sort_order = COALESCE((v_obj->>'SortOrder')::int, 0),
                updated_at = now()
            WHERE layout_objects_id = v_id;
        ELSE
            INSERT INTO layout_objects (layout_objects_id, tenants_id, events_id, object_type,
                label, grid_row, grid_col, row_span, col_span, color, sort_order,
                created_at, updated_at)
            VALUES (
                COALESCE(v_id, gen_random_uuid()), v_tenant, p_event_id,
                v_obj->>'ObjectType',
                NULLIF(v_obj->>'Label', ''),
                (v_obj->>'GridRow')::int,
                (v_obj->>'GridCol')::int,
                COALESCE((v_obj->>'RowSpan')::int, 1),
                COALESCE((v_obj->>'ColSpan')::int, 1),
                NULLIF(v_obj->>'Color', ''),
                COALESCE((v_obj->>'SortOrder')::int, 0),
                now(), now()
            );
        END IF;
    END LOOP;
END; $$;
