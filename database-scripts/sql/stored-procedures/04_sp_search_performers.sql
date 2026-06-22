CREATE OR REPLACE FUNCTION sp_search_performers(p_q text, p_offset int, p_limit int)
RETURNS SETOF vw_performers LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT v.*
    FROM vw_performers v
    WHERE p_q IS NULL OR length(p_q) = 0 OR v.name ILIKE '%' || p_q || '%'
    ORDER BY v.upcoming_event_count DESC, v.name ASC
    OFFSET COALESCE(p_offset, 0)
    LIMIT LEAST(COALESCE(p_limit, 20), 100);
$$;
