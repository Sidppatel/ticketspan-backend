DROP FUNCTION IF EXISTS sp_set_tenant_tier(uuid, text);

CREATE OR REPLACE FUNCTION sp_set_tenant_tier(p_tenants_id uuid, p_tier text)
RETURNS text
LANGUAGE plpgsql
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_old text;
BEGIN
    IF p_tier NOT IN ('free','starter','professional','business','enterprise') THEN
        RAISE EXCEPTION 'invalid tier: %', p_tier;
    END IF;
    SELECT tier INTO v_old FROM tenants WHERE tenants_id = p_tenants_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'tenant not found: %', p_tenants_id;
    END IF;
    UPDATE tenants SET tier = p_tier WHERE tenants_id = p_tenants_id;
    RETURN v_old;
END; $$;
