CREATE OR REPLACE FUNCTION sp_accept_invitation(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE invitations
    SET status = 'Accepted',
        accepted_at = now(),
        updated_at = now()
    WHERE invitations_id = p_id;
END; $$;