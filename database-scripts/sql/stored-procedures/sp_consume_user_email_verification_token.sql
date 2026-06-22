CREATE OR REPLACE FUNCTION sp_consume_user_email_verification_token(
    p_token_hash text
) RETURNS SETOF users LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_user_id uuid;
BEGIN
    SELECT users_id INTO v_user_id
    FROM user_email_verification_tokens
    WHERE token_hash = p_token_hash
      AND used_at IS NULL
      AND expires_at > now()
    LIMIT 1;

    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'Invalid or expired token';
    END IF;

    UPDATE user_email_verification_tokens
    SET used_at = now(),
        updated_at = now()
    WHERE token_hash = p_token_hash;

    UPDATE users
    SET email_verified = true,
        email_verified_at = now(),
        updated_at = now()
    WHERE user_email_verification_tokens_id = v_user_id;

    RETURN QUERY SELECT * FROM users WHERE users_id = v_user_id;
END;
$$;
