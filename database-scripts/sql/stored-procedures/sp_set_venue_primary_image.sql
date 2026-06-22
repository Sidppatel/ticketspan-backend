CREATE OR REPLACE FUNCTION sp_set_venue_primary_image(
    p_venue_id uuid,
    p_image_id uuid
) RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_exists boolean;
BEGIN
    SELECT EXISTS(
        SELECT 1 FROM venue_images WHERE venues_id = p_venue_id AND images_id = p_image_id
    ) INTO v_exists;

    IF NOT v_exists THEN
        RETURN false;
    END IF;

    UPDATE venue_images
    SET is_primary = false, updated_at = now()
    WHERE venues_id = p_venue_id AND is_primary = true;

    UPDATE venue_images
    SET is_primary = true, updated_at = now()
    WHERE venues_id = p_venue_id AND images_id = p_image_id;

    RETURN true;
END; $$;
