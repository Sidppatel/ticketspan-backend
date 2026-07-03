DROP FUNCTION IF EXISTS sp_set_tenant_advanced_reporting(uuid, boolean);

CREATE OR REPLACE FUNCTION sp_set_tenant_advanced_reporting(p_tenants_id uuid, p_enabled boolean)
RETURNS boolean
LANGUAGE plpgsql
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_old boolean;
BEGIN
    SELECT advanced_reporting_enabled INTO v_old FROM tenants WHERE tenants_id = p_tenants_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'tenant not found: %', p_tenants_id;
    END IF;
    UPDATE tenants SET advanced_reporting_enabled = p_enabled WHERE tenants_id = p_tenants_id;
    RETURN v_old;
END; $$;
