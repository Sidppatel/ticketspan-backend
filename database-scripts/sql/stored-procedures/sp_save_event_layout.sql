CREATE OR REPLACE FUNCTION sp_save_event_layout(
    p_event_id uuid, p_grid_rows int, p_grid_cols int,
    p_tables jsonb, p_locked_ids uuid[]
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_request_ids uuid[];
    v_table jsonb;
    v_id uuid;
    v_tenant uuid;
BEGIN
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_event_id;

    UPDATE events SET grid_rows = p_grid_rows, grid_cols = p_grid_cols, updated_at = now()
    WHERE events_id = p_event_id;

    SELECT COALESCE(array_agg((t->>'Id')::uuid) FILTER (WHERE t->>'Id' IS NOT NULL), '{}')
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
                updated_at = now()
            WHERE tables_id = v_id;
        ELSE
            INSERT INTO tables (tables_id, tenants_id, events_id, event_tables_id, label,
                grid_row, grid_col, is_active, sort_order, status,
                row_span, col_span, created_at, updated_at)
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
                now(), now()
            );
        END IF;
    END LOOP;
END; $$;
