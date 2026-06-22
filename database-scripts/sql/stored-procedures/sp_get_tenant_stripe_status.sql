CREATE OR REPLACE FUNCTION sp_get_tenant_stripe_status(
    p_tenants_id uuid
) RETURNS SETOF tenants LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    SELECT * FROM tenants
    WHERE tenants_id = p_tenants_id
      AND archived_at IS NULL;
END; $$;
