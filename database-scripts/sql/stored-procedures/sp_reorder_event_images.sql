CREATE OR REPLACE FUNCTION sp_reorder_event_images(
    p_event_id uuid,
    p_image_ids uuid[]
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    i int;
BEGIN
    FOR i IN 1 .. array_length(p_image_ids, 1) LOOP
        UPDATE event_images
        SET sort_order = i - 1, updated_at = now()
        WHERE events_id = p_event_id AND images_id = p_image_ids[i];
    END LOOP;
END; $$;
