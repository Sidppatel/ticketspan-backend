CREATE OR REPLACE FUNCTION sp_remove_event_image(
    p_event_id uuid,
    p_image_id uuid
) RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_was_primary boolean;
    v_next_image_id uuid;
BEGIN
    SELECT is_primary INTO v_was_primary
    FROM event_images
    WHERE events_id = p_event_id AND images_id = p_image_id;

    IF v_was_primary IS NULL THEN
        RETURN false;
    END IF;

    DELETE FROM event_images
    WHERE events_id = p_event_id AND images_id = p_image_id;

    DELETE FROM images WHERE images_id = p_image_id;

    IF v_was_primary THEN
        SELECT images_id INTO v_next_image_id
        FROM event_images
        WHERE events_id = p_event_id
        ORDER BY sort_order ASC
        LIMIT 1;

        IF v_next_image_id IS NOT NULL THEN
            UPDATE event_images
            SET is_primary = true, updated_at = now()
            WHERE events_id = p_event_id AND images_id = v_next_image_id;
        END IF;
    END IF;

    RETURN true;
END; $$;
