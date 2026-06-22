CREATE OR REPLACE FUNCTION sp_remove_venue_image(
    p_venue_id uuid,
    p_image_id uuid
) RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_was_primary boolean;
    v_next_image_id uuid;
BEGIN
    SELECT is_primary INTO v_was_primary
    FROM venue_images
    WHERE venues_id = p_venue_id AND images_id = p_image_id;

    IF v_was_primary IS NULL THEN
        RETURN false;
    END IF;

    DELETE FROM venue_images WHERE venues_id = p_venue_id AND images_id = p_image_id;
    DELETE FROM images WHERE images_id = p_image_id;

    IF v_was_primary THEN
        SELECT images_id INTO v_next_image_id
        FROM venue_images
        WHERE venues_id = p_venue_id
        ORDER BY sort_order ASC
        LIMIT 1;

        IF v_next_image_id IS NOT NULL THEN
            UPDATE venue_images
            SET is_primary = true, updated_at = now()
            WHERE venues_id = p_venue_id AND images_id = v_next_image_id;
        END IF;
    END IF;

    RETURN true;
END; $$;
