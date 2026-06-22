CREATE OR REPLACE FUNCTION sp_assign_user_event(
    p_users_id uuid, p_events_id uuid, p_assigned_by_users_id uuid DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO user_events (users_id, events_id, assigned_by_users_id, created_at, updated_at)
    VALUES (p_users_id, p_events_id, p_assigned_by_users_id, now(), now())
    ON CONFLICT (users_id, events_id) DO UPDATE SET updated_at = now()
    RETURNING user_events_id INTO v_id;
    RETURN v_id;
END; $$;
