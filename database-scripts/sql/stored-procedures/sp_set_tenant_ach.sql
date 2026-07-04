DROP FUNCTION IF EXISTS sp_set_tenant_ach(uuid, boolean, uuid);

-- Developer gate for ACH: sets whether a tenant may offer ACH and which fee formula
-- replaces the service fee when a buyer pays by ACH. Writes are developer-only via
-- the tenants RLS policy. Returns the previous enabled flag for audit.
CREATE OR REPLACE FUNCTION sp_set_tenant_ach(
    p_tenants_id uuid, p_enabled boolean, p_fee_formulas_id uuid
) RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_old boolean;
BEGIN
    SELECT ach_enabled INTO v_old FROM tenants WHERE tenants_id = p_tenants_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'tenant not found: %', p_tenants_id;
    END IF;
    IF p_enabled AND p_fee_formulas_id IS NULL THEN
        RAISE EXCEPTION 'an ACH fee formula is required to enable ACH' USING ERRCODE = '22023';
    END IF;
    UPDATE tenants
       SET ach_enabled = p_enabled,
           ach_fee_formulas_id = CASE WHEN p_enabled THEN p_fee_formulas_id ELSE ach_fee_formulas_id END,
           updated_at = now()
     WHERE tenants_id = p_tenants_id;
    RETURN COALESCE(v_old, false);
END; $$;
