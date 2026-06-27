DROP FUNCTION IF EXISTS sp_update_table(uuid, text, uuid, int, int, bool, int);
DROP FUNCTION IF EXISTS sp_update_table(uuid, text, uuid, int, int, bool, int, int, int);

CREATE OR REPLACE FUNCTION sp_update_table(
    p_id uuid, p_label text, p_event_table_id uuid,
    p_pos_x numeric, p_pos_y numeric, p_is_active bool, p_sort_order int,
    p_width numeric DEFAULT NULL, p_height numeric DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tables SET
        label = COALESCE(p_label, label),
        event_tables_id = COALESCE(p_event_table_id, event_tables_id),
        pos_x = COALESCE(p_pos_x, pos_x),
        pos_y = COALESCE(p_pos_y, pos_y),
        is_active = COALESCE(p_is_active, is_active),
        sort_order = COALESCE(p_sort_order, sort_order),
        width = COALESCE(p_width, width),
        height = COALESCE(p_height, height),
        updated_at = now()
    WHERE tables_id = p_id;
END; $$;
