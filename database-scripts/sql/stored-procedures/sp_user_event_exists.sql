CREATE OR REPLACE FUNCTION sp_user_event_exists(
    p_users_id uuid, p_events_id uuid
) RETURNS boolean LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT EXISTS(
        SELECT 1 FROM user_events
        WHERE users_id = p_users_id AND events_id = p_events_id
    );
$$;
