CREATE OR REPLACE FUNCTION sp_invalidate_password_reset_token(p_token_hash text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE password_reset_tokens
    SET is_used = true, used_at = now(), updated_at = now()
    WHERE token_hash = p_token_hash;
END; $$;
