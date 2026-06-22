CREATE OR REPLACE FUNCTION sp_update_user_password(
    p_user_id uuid,
    p_password_hash text
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM users WHERE users_id = p_user_id) THEN
        RAISE EXCEPTION 'User not found';
    END IF;

    UPDATE users
    SET password_hash = p_password_hash,
        email_verified = true,
        updated_at = now()
    WHERE users_id = p_user_id;
END;
$$;
