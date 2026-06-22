CREATE OR REPLACE FUNCTION sp_delete_image(p_image_id uuid) RETURNS text LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_key text;
BEGIN
    DELETE FROM images WHERE images_id = p_image_id RETURNING storage_key INTO v_key;
    RETURN v_key;
END; $$;