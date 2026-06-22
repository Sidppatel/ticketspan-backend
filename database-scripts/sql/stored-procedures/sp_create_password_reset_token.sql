CREATE OR REPLACE FUNCTION sp_create_password_reset_token(
    p_users_id uuid,
    p_token_hash text,
    p_expires_at timestamptz,
    p_email text DEFAULT NULL,
    p_ip_address text DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO password_reset_tokens (
        users_id, token_hash, expires_at, email, ip_address, is_used, created_at, updated_at
    ) VALUES (
        p_users_id, p_token_hash, p_expires_at, p_email, p_ip_address, false, now(), now()
    )
    RETURNING password_reset_tokens_id INTO v_id;
    RETURN v_id;
END; $$;
