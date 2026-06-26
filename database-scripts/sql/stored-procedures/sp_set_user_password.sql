CREATE OR REPLACE FUNCTION sp_set_user_password(
    p_users_id uuid,
    p_new_password_hash text,
    p_pepper_version smallint DEFAULT 1,
    p_revoke_other_sessions boolean DEFAULT true,
    p_current_session_hash text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE users
    SET password_hash = p_new_password_hash,
        pepper_version = p_pepper_version,
        updated_at = now()
    WHERE users_id = p_users_id;

    IF p_revoke_other_sessions THEN
        UPDATE device_sessions
        SET revoked_at = now(),
            updated_at = now()
        WHERE users_id = p_users_id
          AND revoked_at IS NULL
          AND (p_current_session_hash IS NULL OR session_hash <> p_current_session_hash);
    END IF;
END; $$;
