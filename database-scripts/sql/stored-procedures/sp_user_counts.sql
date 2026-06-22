CREATE OR REPLACE FUNCTION sp_user_counts()
RETURNS TABLE(total integer, active integer, new_this_month integer)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        COUNT(*)::integer AS total,
        COUNT(*) FILTER (WHERE is_active = true)::integer AS active,
        COUNT(*) FILTER (WHERE created_at >= date_trunc('month', now()))::integer AS new_this_month
    FROM users;
$$;
