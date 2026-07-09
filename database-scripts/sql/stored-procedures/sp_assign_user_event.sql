CREATE OR REPLACE FUNCTION sp_assign_user_event(
    p_users_id uuid, p_events_id uuid, p_assigned_by_users_id uuid DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO staff_event_access (staff_user_id, event_id, assigned_by_admin_id, created_at, updated_at)
    VALUES (p_users_id, p_events_id, p_assigned_by_users_id, now(), now())
    ON CONFLICT (staff_user_id, event_id) DO UPDATE SET updated_at = now()
    RETURNING staff_event_access_id INTO v_id;
    RETURN v_id;
END; $$;
