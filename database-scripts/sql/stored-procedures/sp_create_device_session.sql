CREATE OR REPLACE FUNCTION sp_create_device_session(
    p_users_id uuid, p_session_hash text, p_fingerprint text,
    p_device_name text, p_ip text, p_expires_at timestamptz
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO device_sessions (users_id, session_hash, device_fingerprint,
        device_name, ip_address, last_activity_at, expires_at, created_at, updated_at)
    VALUES (p_users_id, p_session_hash, p_fingerprint,
        p_device_name, p_ip, now(), p_expires_at, now(), now())
    RETURNING device_sessions_id INTO v_id;
    RETURN v_id;
END; $$;
