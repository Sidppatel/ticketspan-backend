CREATE OR REPLACE FUNCTION sp_update_tenant_stripe_status(
    p_stripe_account_id text,
    p_charges_enabled boolean,
    p_payouts_enabled boolean,
    p_details_submitted boolean,
    p_requirements_due_json jsonb DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tenants
    SET stripe_charges_enabled   = p_charges_enabled,
        stripe_payouts_enabled   = p_payouts_enabled,
        stripe_details_submitted = p_details_submitted,
        stripe_requirements_due  = COALESCE(p_requirements_due_json, stripe_requirements_due),
        stripe_onboarded_at      = CASE
            WHEN stripe_onboarded_at IS NULL AND p_details_submitted = true THEN now()
            ELSE stripe_onboarded_at END,
        updated_at = now()
    WHERE stripe_connected_account_id = p_stripe_account_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No tenant with Stripe account %', p_stripe_account_id
            USING ERRCODE = 'no_data_found';
    END IF;
END; $$;
