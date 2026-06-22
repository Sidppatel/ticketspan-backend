CREATE OR REPLACE FUNCTION sp_cleanup_expired_locks() RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_count int;
BEGIN
    UPDATE tables SET status = 'Available', locked_by_users_id = NULL,
        lock_expires_at = NULL, updated_at = now()
    WHERE status = 'Locked' AND lock_expires_at < now();
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;