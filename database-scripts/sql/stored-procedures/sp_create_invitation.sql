CREATE OR REPLACE FUNCTION sp_create_invitation(
    p_email text, p_token_hash text, p_role smallint,
    p_invited_by uuid, p_expires_at timestamptz,
    p_tenants_id uuid DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO invitations (tenants_id, email, token_hash, role,
        invited_by_users_id, status, expires_at, created_at, updated_at)
    VALUES (p_tenants_id, p_email, p_token_hash, p_role,
        p_invited_by, 'Pending', p_expires_at, now(), now())
    RETURNING invitations_id INTO v_id;
    RETURN v_id;
END; $$;
