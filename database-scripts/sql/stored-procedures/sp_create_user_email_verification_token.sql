CREATE OR REPLACE FUNCTION sp_create_user_email_verification_token(
    p_user_id uuid,
    p_token_hash text,
    p_expires_at timestamptz,
    p_ip_address text
) RETURNS SETOF user_email_verification_tokens LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
BEGIN
    INSERT INTO user_email_verification_tokens (
        users_id, token_hash, expires_at, ip_address
    ) VALUES (
        p_user_id, p_token_hash, p_expires_at, p_ip_address
    )
    RETURNING user_email_verification_tokens_id INTO v_id;

    RETURN QUERY SELECT * FROM user_email_verification_tokens WHERE user_email_verification_tokens_id = v_id;
END;
$$;
