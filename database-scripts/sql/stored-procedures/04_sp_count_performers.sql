CREATE OR REPLACE FUNCTION sp_count_performers(p_q text)
RETURNS int LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT COUNT(*)::int
    FROM performers p
    WHERE p_q IS NULL OR length(p_q) = 0 OR p.name ILIKE '%' || p_q || '%';
$$;
