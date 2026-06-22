CREATE OR REPLACE FUNCTION sp_get_email_logs(
    p_recipient text, p_skip int, p_take int
) RETURNS SETOF email_logs LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM email_logs
    WHERE p_recipient IS NULL OR recipient ILIKE '%' || p_recipient || '%'
    ORDER BY timestamp DESC
    OFFSET p_skip LIMIT p_take;
$$;

CREATE OR REPLACE FUNCTION sp_count_email_logs(p_recipient text)
RETURNS int LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT COUNT(*)::int FROM email_logs
    WHERE p_recipient IS NULL OR recipient ILIKE '%' || p_recipient || '%';
$$;
