CREATE OR REPLACE FUNCTION sp_count_tenants(
    p_search text DEFAULT NULL,
    p_include_archived boolean DEFAULT false
) RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_search text;
    v_count int;
BEGIN
    v_search := NULLIF(trim(coalesce(p_search, '')), '');
    SELECT count(*)::int INTO v_count
    FROM tenants t
    WHERE (p_include_archived OR t.archived_at IS NULL)
      AND (
        v_search IS NULL
        OR t.name ILIKE '%' || v_search || '%'
        OR t.legal_name ILIKE '%' || v_search || '%'
        OR t.slug ILIKE '%' || v_search || '%'
      );
    RETURN v_count;
END; $$;
