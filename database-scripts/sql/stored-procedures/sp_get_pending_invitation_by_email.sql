CREATE OR REPLACE FUNCTION sp_get_pending_invitation_by_email(p_email text)
RETURNS SETOF invitations
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM invitations
    WHERE email = p_email
      AND status = 'Pending'
      AND expires_at > now()
    LIMIT 1;
$$;