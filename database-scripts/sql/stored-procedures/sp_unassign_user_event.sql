CREATE OR REPLACE FUNCTION sp_unassign_user_event(
    p_users_id uuid, p_events_id uuid
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    DELETE FROM staff_event_access
    WHERE staff_user_id = p_users_id AND event_id = p_events_id;

    DELETE FROM invitations
    WHERE invitations_id = p_users_id AND event_id = p_events_id AND status = 'Pending';
END; $$;
