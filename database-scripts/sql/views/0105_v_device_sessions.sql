CREATE OR REPLACE VIEW vw_device_sessions AS
SELECT
    device_sessions_id AS device_session_id, users_id, session_hash,
    device_fingerprint, device_name, ip_address,
    last_activity_at, expires_at, revoked_at,
    created_at, updated_at
FROM device_sessions;
