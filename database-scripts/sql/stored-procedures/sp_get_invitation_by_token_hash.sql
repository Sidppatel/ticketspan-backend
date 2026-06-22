CREATE OR REPLACE FUNCTION sp_get_invitation_by_token_hash(p_token_hash text)
RETURNS SETOF invitations
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM invitations
    WHERE token_hash = p_token_hash
      AND status = 'Pending'
      AND expires_at > now()
    LIMIT 1;
$$;