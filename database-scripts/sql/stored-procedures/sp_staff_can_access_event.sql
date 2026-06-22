CREATE OR REPLACE FUNCTION sp_staff_can_access_event(
    p_business_user_id uuid, p_event_id uuid, p_grace_hours int DEFAULT 24
) RETURNS boolean LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT EXISTS(
        SELECT 1
        FROM user_events aue
        JOIN users au ON au.users_id = aue.users_id
        JOIN events e ON e.events_id = aue.events_id
        WHERE aue.users_id = p_business_user_id
          AND aue.events_id = p_event_id
          AND au.is_active = true
          AND now() <= e.end_date + make_interval(hours => p_grace_hours)
    );
$$;
