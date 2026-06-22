CREATE OR REPLACE FUNCTION sp_set_platform_primary_image(p_image_id uuid)
RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_exists boolean;
BEGIN
    SELECT EXISTS(SELECT 1 FROM platform_images WHERE images_id = p_image_id)
    INTO v_exists;

    IF NOT v_exists THEN
        RETURN false;
    END IF;

    UPDATE platform_images SET is_primary = false, updated_at = now() WHERE is_primary = true;
    UPDATE platform_images SET is_primary = true, updated_at = now() WHERE images_id = p_image_id;

    RETURN true;
END; $$;
