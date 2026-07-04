CREATE OR REPLACE FUNCTION sp_list_staff_for_event(p_event_id uuid)
RETURNS TABLE(
    business_user_event_id uuid,
    users_id uuid,
    first_name text,
    last_name text,
    email text,
    business_user_is_active boolean,
    events_id uuid,
    event_title text,
    event_slug text,
    start_date timestamptz,
    end_date timestamptz,
    event_status text,
    assigned_by_users_id uuid,
    created_at timestamptz,
    updated_at timestamptz,
    user_role integer
) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        aue.staff_event_access_id, aue.staff_user_id,
        au.first_name::text, au.last_name::text, au.email::text,
        au.is_active,
        aue.event_id, e.title::text, e.slug::text,
        e.start_date, e.end_date, e.status::text,
        aue.assigned_by_admin_id,
        aue.created_at, aue.updated_at,
        au.role
    FROM staff_event_access aue
    JOIN users au ON au.users_id = aue.staff_user_id
    JOIN events e ON e.events_id = aue.event_id
    WHERE aue.event_id = p_event_id
      AND au.is_active = true
    ORDER BY au.first_name, au.last_name;
$$;
