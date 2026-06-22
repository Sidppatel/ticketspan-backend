DROP FUNCTION IF EXISTS sp_update_table(uuid, text, uuid, int, int, bool, int);

CREATE OR REPLACE FUNCTION sp_update_table(
    p_id uuid, p_label text, p_event_table_id uuid,
    p_grid_row int, p_grid_col int, p_is_active bool, p_sort_order int,
    p_row_span int DEFAULT NULL, p_col_span int DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tables SET
        label = COALESCE(p_label, label),
        event_tables_id = COALESCE(p_event_table_id, event_tables_id),
        grid_row = COALESCE(p_grid_row, grid_row),
        grid_col = COALESCE(p_grid_col, grid_col),
        is_active = COALESCE(p_is_active, is_active),
        sort_order = COALESCE(p_sort_order, sort_order),
        row_span = COALESCE(p_row_span, row_span),
        col_span = COALESCE(p_col_span, col_span),
        updated_at = now()
    WHERE tables_id = p_id;
END; $$;
