CREATE OR REPLACE FUNCTION sp_release_table_lock(
    p_user_id uuid, p_event_id uuid, p_table_id uuid
) RETURNS bool LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_event_id uuid := p_event_id;
BEGIN
    IF v_event_id IS NULL THEN
        SELECT events_id INTO v_event_id FROM tables WHERE tables_id = p_table_id;
    END IF;

    UPDATE tables SET status = 'Available', locked_by_users_id = NULL,
        lock_expires_at = NULL, updated_at = now()
    WHERE tables_id = p_table_id AND (v_event_id IS NULL OR events_id = v_event_id)
      AND locked_by_users_id = p_user_id AND status = 'Locked';
    RETURN FOUND;
END; $$;