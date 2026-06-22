CREATE OR REPLACE FUNCTION sp_set_user_active(p_users_id uuid, p_is_active bool)
RETURNS bool LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_found bool;
BEGIN
    UPDATE users SET is_active = p_is_active, updated_at = now()
    WHERE users_id = p_users_id
    RETURNING true INTO v_found;

    RETURN COALESCE(v_found, false);
END; $$;
