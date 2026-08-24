DROP FUNCTION IF EXISTS sp_set_tenant_tax_default(uuid, bool);

CREATE OR REPLACE FUNCTION sp_set_tenant_tax_default(
    p_tenants_id uuid, p_charge_tax bool
) RETURNS jsonb LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_old_val bool;
BEGIN
    SELECT COALESCE(charge_tax_by_default, true) INTO v_old_val
      FROM tenants WHERE tenants_id = p_tenants_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'tenant not found: %', p_tenants_id;
    END IF;
    UPDATE tenants
       SET charge_tax_by_default = p_charge_tax,
           updated_at = now()
     WHERE tenants_id = p_tenants_id;
    RETURN jsonb_build_object('charge_tax_by_default', v_old_val);
END; $$;
