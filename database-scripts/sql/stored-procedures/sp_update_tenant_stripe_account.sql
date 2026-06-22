CREATE OR REPLACE FUNCTION sp_update_tenant_stripe_account(
    p_tenants_id uuid,
    p_stripe_account_id text
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_existing text;
BEGIN
    SELECT stripe_connected_account_id INTO v_existing
    FROM tenants WHERE tenants_id = p_tenants_id FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Tenant % not found', p_tenants_id USING ERRCODE = 'no_data_found';
    END IF;

    IF v_existing IS NOT NULL AND v_existing <> p_stripe_account_id THEN
        RAISE EXCEPTION 'Tenant % already has Stripe account %', p_tenants_id, v_existing
            USING ERRCODE = 'unique_violation';
    END IF;

    UPDATE tenants
    SET stripe_connected_account_id = p_stripe_account_id, updated_at = now()
    WHERE tenants_id = p_tenants_id;
END; $$;
