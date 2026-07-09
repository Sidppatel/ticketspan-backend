CREATE OR REPLACE FUNCTION sp_accept_invitation(p_id uuid)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE invitations
    SET status = 'Accepted',
        accepted_at = now(),
        updated_at = now()
    WHERE invitations_id = p_id;
END; $$;

CREATE OR REPLACE FUNCTION sp_get_invitation_by_token(p_token_hash text)
RETURNS TABLE(invitations_id uuid, email text, role smallint, tenants_id uuid, event_id uuid)
LANGUAGE sql STABLE SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT i.invitations_id, i.email::text, i.role, i.tenants_id, i.event_id
      FROM invitations i
     WHERE i.token_hash = p_token_hash
       AND i.status = 'Pending'
       AND i.expires_at > now();
$$;