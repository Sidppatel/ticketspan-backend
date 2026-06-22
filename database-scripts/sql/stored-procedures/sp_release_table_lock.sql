CREATE OR REPLACE FUNCTION sp_release_table_lock(
    p_user_id uuid, p_event_id uuid, p_table_id uuid
) RETURNS bool LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tables SET status = 'Available', locked_by_users_id = NULL,
        lock_expires_at = NULL, updated_at = now()
    WHERE tables_id = p_table_id AND events_id = p_event_id
      AND locked_by_users_id = p_user_id AND status = 'Locked';
    RETURN FOUND;
END; $$;