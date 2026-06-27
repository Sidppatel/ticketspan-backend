DROP FUNCTION IF EXISTS sp_create_table(uuid, uuid, text, int, int, int, int, int);

CREATE OR REPLACE FUNCTION sp_create_table(
    p_event_table_id uuid, p_event_id uuid, p_label text,
    p_pos_x numeric, p_pos_y numeric, p_sort_order int,
    p_width numeric DEFAULT 80, p_height numeric DEFAULT 80
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO tables (tenants_id, event_tables_id, events_id, label, pos_x, pos_y,
        sort_order, width, height, is_active, status, created_at, updated_at)
    VALUES ((SELECT tenants_id FROM events WHERE events_id = p_event_id),
        p_event_table_id, p_event_id, p_label,
        p_pos_x, p_pos_y, p_sort_order,
        COALESCE(p_width, 80), COALESCE(p_height, 80),
        true, 'Available', now(), now())
    RETURNING tables_id INTO v_id;
    RETURN v_id;
END; $$;
