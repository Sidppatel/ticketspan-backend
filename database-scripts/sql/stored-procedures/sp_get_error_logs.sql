CREATE OR REPLACE FUNCTION sp_get_error_logs(
    p_severity text, p_source text, p_resolved boolean, p_search text,
    p_from timestamptz, p_to timestamptz, p_skip int, p_take int
) RETURNS TABLE(
    id uuid,
    "timestamp" timestamptz,
    severity text,
    message text,
    exception_type text,
    stack_trace text,
    request_path text,
    request_method text,
    status_code int,
    users_id uuid,
    ip_address text,
    correlation_id text,
    metadata_json text,
    tenants_id uuid,
    source text,
    resolved boolean,
    resolved_notes text,
    resolved_by text,
    resolved_at timestamptz
) LANGUAGE plpgsql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    SELECT dl.id, dl.timestamp, dl.severity, dl.message, dl.exception_type, dl.stack_trace,
           dl.request_path, dl.request_method, dl.status_code, dl.users_id, dl.ip_address,
           dl.correlation_id, dl.metadata_json, dl.tenants_id, dl.source, dl.resolved,
           dl.resolved_notes, dl.resolved_by, dl.resolved_at
    FROM vw_developer_logs dl
    WHERE (p_severity IS NULL OR dl.severity = p_severity)
      AND (p_source IS NULL OR dl.source = p_source)
      AND (p_resolved IS NULL OR dl.resolved = p_resolved)
      AND (p_search IS NULL OR dl.message ILIKE '%' || p_search || '%'
           OR (dl.request_path IS NOT NULL AND dl.request_path ILIKE '%' || p_search || '%')
           OR dl.id::text = p_search
           OR (dl.correlation_id IS NOT NULL AND dl.correlation_id = p_search))
      AND (p_from IS NULL OR dl.timestamp >= p_from)
      AND (p_to IS NULL OR dl.timestamp <= p_to)
    ORDER BY dl.timestamp DESC
    OFFSET p_skip LIMIT p_take;
END;
$$;

CREATE OR REPLACE FUNCTION sp_count_error_logs(
    p_severity text, p_source text, p_resolved boolean, p_search text,
    p_from timestamptz, p_to timestamptz
) RETURNS int LANGUAGE plpgsql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_count int;
BEGIN
    SELECT COUNT(*)::int INTO v_count
    FROM vw_developer_logs dl
    WHERE (p_severity IS NULL OR dl.severity = p_severity)
      AND (p_source IS NULL OR dl.source = p_source)
      AND (p_resolved IS NULL OR dl.resolved = p_resolved)
      AND (p_search IS NULL OR dl.message ILIKE '%' || p_search || '%'
           OR (dl.request_path IS NOT NULL AND dl.request_path ILIKE '%' || p_search || '%')
           OR dl.id::text = p_search
           OR (dl.correlation_id IS NOT NULL AND dl.correlation_id = p_search))
      AND (p_from IS NULL OR dl.timestamp >= p_from)
      AND (p_to IS NULL OR dl.timestamp <= p_to);
      
    RETURN v_count;
END;
$$;
