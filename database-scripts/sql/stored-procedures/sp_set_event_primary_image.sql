CREATE OR REPLACE FUNCTION sp_set_event_primary_image(
    p_event_id uuid,
    p_image_id uuid
) RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_exists boolean;
BEGIN
    SELECT EXISTS(
        SELECT 1 FROM event_images
        WHERE events_id = p_event_id AND images_id = p_image_id
    ) INTO v_exists;

    IF NOT v_exists THEN
        RETURN false;
    END IF;

    UPDATE event_images
    SET is_primary = false, updated_at = now()
    WHERE events_id = p_event_id AND is_primary = true;

    UPDATE event_images
    SET is_primary = true, updated_at = now()
    WHERE events_id = p_event_id AND images_id = p_image_id;

    RETURN true;
END; $$;
