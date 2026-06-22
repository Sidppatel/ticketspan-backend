CREATE OR REPLACE FUNCTION sp_create_table(
    p_event_table_id uuid, p_event_id uuid, p_label text,
    p_grid_row int, p_grid_col int, p_sort_order int,
    p_row_span int DEFAULT 1, p_col_span int DEFAULT 1
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO tables (tenants_id, event_tables_id, events_id, label, grid_row, grid_col,
        sort_order, row_span, col_span, is_active, status, created_at, updated_at)
    VALUES ((SELECT tenants_id FROM events WHERE events_id = p_event_id),
        p_event_table_id, p_event_id, p_label,
        p_grid_row, p_grid_col, p_sort_order,
        COALESCE(p_row_span, 1), COALESCE(p_col_span, 1),
        true, 'Available', now(), now())
    RETURNING tables_id INTO v_id;
    RETURN v_id;
END; $$;
