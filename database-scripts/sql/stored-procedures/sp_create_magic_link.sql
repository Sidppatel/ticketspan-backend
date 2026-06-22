CREATE OR REPLACE FUNCTION sp_create_magic_link(
    p_email text, p_token_hash text, p_expires_at timestamptz,
    p_tenants_id uuid DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO magic_link_tokens (tenants_id, token_hash, email, expires_at, is_used, created_at, updated_at)
    VALUES (p_tenants_id, p_token_hash, p_email, p_expires_at, false, now(), now())
    RETURNING magic_link_tokens_id INTO v_id;
    RETURN v_id;
END; $$;
