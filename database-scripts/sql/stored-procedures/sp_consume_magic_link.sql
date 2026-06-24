CREATE OR REPLACE FUNCTION sp_consume_magic_link(p_token_hash text)
RETURNS TABLE (
    id uuid, email text, expires_at timestamptz, tenants_id uuid
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_email text;
BEGIN
    UPDATE magic_link_tokens AS t
    SET is_used = true, used_at = now(), updated_at = now()
    WHERE t.token_hash = p_token_hash AND t.is_used = false AND t.expires_at > now()
    RETURNING t.email INTO v_email;

    IF v_email IS NULL THEN
        RETURN;
    END IF;

    UPDATE users AS u
    SET email_verified = true,
        email_verified_at = COALESCE(u.email_verified_at, now()),
        updated_at = now()
    WHERE u.email = v_email AND u.email_verified = false;

    RETURN QUERY
    SELECT t.magic_link_tokens_id, t.email::text, t.expires_at, t.tenants_id
    FROM magic_link_tokens AS t
    WHERE t.token_hash = p_token_hash;
END; $$;
