CREATE OR REPLACE FUNCTION sp_clear_tenant_stripe_account(
    p_tenants_id uuid
) RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_rows int;
BEGIN
    UPDATE tenants
       SET stripe_connected_account_id = NULL,
           stripe_charges_enabled = false,
           stripe_payouts_enabled = false,
           stripe_details_submitted = false,
           stripe_onboarded_at = NULL,
           stripe_requirements_due = NULL,
           updated_at = now()
     WHERE tenants_id = p_tenants_id;
    GET DIAGNOSTICS v_rows = ROW_COUNT;
    RETURN v_rows;
END; $$;
