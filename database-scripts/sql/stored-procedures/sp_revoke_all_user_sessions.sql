CREATE OR REPLACE FUNCTION sp_revoke_all_user_sessions(
    p_users_id uuid, p_except_hash text DEFAULT NULL
) RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_count int;
BEGIN
    UPDATE device_sessions SET revoked_at = now(), updated_at = now()
    WHERE users_id = p_users_id AND revoked_at IS NULL
      AND (p_except_hash IS NULL OR session_hash <> p_except_hash);
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;
