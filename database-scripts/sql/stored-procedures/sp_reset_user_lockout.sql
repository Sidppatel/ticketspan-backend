CREATE OR REPLACE FUNCTION sp_reset_user_lockout(p_users_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE users SET
        failed_login_attempts = 0,
        locked_until = NULL,
        updated_at = now()
    WHERE users_id = p_users_id;
END; $$;
