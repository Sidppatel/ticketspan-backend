DROP FUNCTION IF EXISTS sp_assign_user_event(uuid, uuid, uuid);
DROP FUNCTION IF EXISTS sp_assign_user_event(uuid, uuid, uuid, timestamptz, timestamptz);

CREATE OR REPLACE FUNCTION sp_assign_user_event(
    p_users_id uuid,
    p_events_id uuid,
    p_assigned_by_users_id uuid DEFAULT NULL,
    p_access_start timestamptz DEFAULT NULL,
    p_access_end timestamptz DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO staff_event_access (
        staff_user_id, event_id, assigned_by_admin_id, access_start, access_end, created_at, updated_at
    )
    VALUES (
        p_users_id, p_events_id, p_assigned_by_users_id, p_access_start, p_access_end, now(), now()
    )
    ON CONFLICT (staff_user_id, event_id) DO UPDATE SET
        assigned_by_admin_id = COALESCE(EXCLUDED.assigned_by_admin_id, staff_event_access.assigned_by_admin_id),
        access_start = EXCLUDED.access_start,
        access_end = EXCLUDED.access_end,
        updated_at = now()
    RETURNING staff_event_access_id INTO v_id;
    RETURN v_id;
END; $$;
