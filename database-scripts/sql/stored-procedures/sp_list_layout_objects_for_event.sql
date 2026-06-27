DROP FUNCTION IF EXISTS sp_list_layout_objects_for_event(uuid);

CREATE OR REPLACE FUNCTION sp_list_layout_objects_for_event(p_event_id uuid)
RETURNS TABLE(layout_objects_id uuid, object_type text, label text,
    pos_x numeric, pos_y numeric, width numeric, height numeric, color text, sort_order int)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT layout_objects_id, object_type, label, pos_x, pos_y,
        width, height, color, sort_order
    FROM layout_objects WHERE events_id = p_event_id
    ORDER BY sort_order, created_at;
$$;
