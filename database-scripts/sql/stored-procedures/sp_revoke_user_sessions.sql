CREATE OR REPLACE FUNCTION sp_revoke_user_sessions(p_users_id uuid) RETURNS void LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE users SET token_version = token_version + 1 WHERE users_id = p_users_id;
    UPDATE device_sessions SET revoked_at = now(), updated_at = now()
    WHERE users_id = p_users_id AND revoked_at IS NULL;
END; $$;
