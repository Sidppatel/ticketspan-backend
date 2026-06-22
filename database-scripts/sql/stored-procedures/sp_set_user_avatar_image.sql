CREATE OR REPLACE FUNCTION sp_set_user_image(
    p_users_id uuid, p_image_id uuid
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_old_image_id uuid;
BEGIN
    SELECT images_id INTO v_old_image_id FROM users WHERE users_id = p_users_id;
    UPDATE users SET images_id = p_image_id, updated_at = now() WHERE users_id = p_users_id;
    RETURN v_old_image_id;
END; $$;
