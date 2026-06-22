CREATE OR REPLACE FUNCTION sp_get_password_reset_token(p_token_hash text)
RETURNS TABLE(
    token_id uuid,
    users_id uuid,
    is_used boolean,
    expires_at timestamptz,
    user_email text
) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        t.password_reset_tokens_id,
        t.users_id,
        t.is_used,
        t.expires_at,
        u.email::text
    FROM password_reset_tokens t
    LEFT JOIN users u ON u.users_id = t.users_id
    WHERE t.token_hash = p_token_hash
    LIMIT 1;
$$;
