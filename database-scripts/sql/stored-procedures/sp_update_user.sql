CREATE OR REPLACE FUNCTION sp_update_user(
    p_users_id uuid, p_first_name text DEFAULT NULL, p_last_name text DEFAULT NULL,
    p_phone text DEFAULT NULL, p_role smallint DEFAULT NULL,
    p_is_active boolean DEFAULT NULL, p_image_id uuid DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE users SET
        first_name = COALESCE(p_first_name, first_name),
        last_name = COALESCE(p_last_name, last_name),
        phone = COALESCE(p_phone, phone),
        role = COALESCE(p_role, role),
        is_active = COALESCE(p_is_active, is_active),
        images_id = COALESCE(p_image_id, images_id),
        updated_at = now()
    WHERE users_id = p_users_id;
END; $$;
