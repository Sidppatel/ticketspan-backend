CREATE OR REPLACE FUNCTION sp_update_session_activity(p_session_hash text) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_users_id uuid;
BEGIN
    UPDATE device_sessions
       SET last_activity_at = now()
     WHERE session_hash = p_session_hash
    RETURNING users_id INTO v_users_id;

    IF v_users_id IS NOT NULL THEN
        UPDATE users SET last_request_at = now() WHERE users_id = v_users_id;
    END IF;
END; $$;
