CREATE OR REPLACE FUNCTION sp_revoke_device_session(p_session_hash text) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE device_sessions SET revoked_at = now(), updated_at = now()
    WHERE session_hash = p_session_hash AND revoked_at IS NULL;
END; $$;
