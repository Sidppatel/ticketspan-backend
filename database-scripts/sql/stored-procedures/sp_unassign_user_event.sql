CREATE OR REPLACE FUNCTION sp_unassign_user_event(
    p_users_id uuid, p_events_id uuid
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    DELETE FROM user_events
    WHERE users_id = p_users_id AND events_id = p_events_id;
END; $$;
