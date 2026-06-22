CREATE OR REPLACE FUNCTION sp_delete_user(p_users_id uuid)
RETURNS bool LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_found bool;
BEGIN
    DELETE FROM users WHERE users_id = p_users_id
    RETURNING true INTO v_found;

    RETURN COALESCE(v_found, false);
END; $$;
