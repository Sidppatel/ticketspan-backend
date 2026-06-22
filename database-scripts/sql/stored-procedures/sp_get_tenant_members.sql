CREATE OR REPLACE FUNCTION sp_get_tenant_members(
    p_tenants_id uuid
) RETURNS TABLE (
    users_id uuid,
    email text,
    role smallint,
    display_name text
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    SELECT
        u.users_id,
        u.email::text,
        u.role,
        NULLIF(TRIM(REGEXP_REPLACE(
            CONCAT(COALESCE(u.first_name, ''), ' ', COALESCE(u.last_name, '')),
            '\s+', ' ', 'g')), '')::text
    FROM users u
    WHERE u.tenants_id = p_tenants_id
      AND u.role IN (1, 2, 3)
    ORDER BY u.email;
END; $$;
