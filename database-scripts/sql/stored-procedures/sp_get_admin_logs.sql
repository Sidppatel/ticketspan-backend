CREATE OR REPLACE FUNCTION sp_get_admin_logs(
    p_action text, p_entity_type text, p_from timestamptz, p_to timestamptz,
    p_skip int, p_take int
) RETURNS SETOF vw_business_logs LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM vw_business_logs
    WHERE (p_action IS NULL OR action ILIKE '%' || p_action || '%')
      AND (p_entity_type IS NULL OR entity_type = p_entity_type)
      AND (p_from IS NULL OR timestamp >= p_from)
      AND (p_to IS NULL OR timestamp <= p_to)
    ORDER BY timestamp DESC
    OFFSET p_skip LIMIT p_take;
$$;

CREATE OR REPLACE FUNCTION sp_count_admin_logs(
    p_action text, p_entity_type text, p_from timestamptz, p_to timestamptz
) RETURNS int LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT COUNT(*)::int FROM vw_business_logs
    WHERE (p_action IS NULL OR action ILIKE '%' || p_action || '%')
      AND (p_entity_type IS NULL OR entity_type = p_entity_type)
      AND (p_from IS NULL OR timestamp >= p_from)
      AND (p_to IS NULL OR timestamp <= p_to);
$$;
