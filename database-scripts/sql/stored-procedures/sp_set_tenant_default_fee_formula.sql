-- Developer-only operation (enforced in the service layer). Sets the tenant's
-- default fee formula applied to every price that does not override it.
CREATE OR REPLACE FUNCTION sp_set_tenant_default_fee_formula(
    p_tenant_id uuid, p_fee_formulas_id uuid
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tenants SET default_fee_formulas_id = p_fee_formulas_id, updated_at = now()
    WHERE tenants_id = p_tenant_id;
END; $$;
