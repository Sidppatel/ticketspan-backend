CREATE OR REPLACE FUNCTION sp_get_tenant_by_user(
    p_users_id uuid
) RETURNS SETOF tenants LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    SELECT t.*
    FROM tenants t
    INNER JOIN users u ON u.tenants_id = t.tenants_id
    WHERE u.users_id = p_users_id
      AND t.archived_at IS NULL;
END; $$;
