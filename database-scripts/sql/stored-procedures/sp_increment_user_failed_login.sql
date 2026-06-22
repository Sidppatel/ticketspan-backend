CREATE OR REPLACE FUNCTION sp_increment_user_failed_login(
    p_users_id uuid, p_max_attempts int, p_lockout_minutes int
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE users SET
        failed_login_attempts = failed_login_attempts + 1,
        locked_until = CASE
            WHEN failed_login_attempts + 1 >= p_max_attempts
                THEN now() + (p_lockout_minutes::text || ' minutes')::interval
            ELSE locked_until
        END,
        updated_at = now()
    WHERE users_id = p_users_id;
END; $$;
