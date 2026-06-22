CREATE OR REPLACE FUNCTION sp_update_user_last_login(p_users_id uuid) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE users SET last_login_at = now(), updated_at = now() WHERE users_id = p_users_id;
END; $$;
