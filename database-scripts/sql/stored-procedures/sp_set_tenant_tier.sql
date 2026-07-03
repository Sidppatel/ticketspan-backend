DROP FUNCTION IF EXISTS sp_set_tenant_tier(uuid, text);

-- Assigns a tenant tier and re-points the tenant's default fee formula at the
-- tier's canonical rates (app.tier_pricing). A manually-set custom formula
-- (name not starting 'tier:') is preserved — a developer fee override outlives
-- tier switches. 'suspended' keeps the previous formula; sales are blocked by
-- app.assert_tenant_sellable.
CREATE OR REPLACE FUNCTION sp_set_tenant_tier(p_tenants_id uuid, p_tier text)
RETURNS text
LANGUAGE plpgsql
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_old text; v_formula uuid; v_current_formula uuid; v_current_name text; t record;
BEGIN
    IF p_tier NOT IN ('free','starter','professional','business','enterprise','trial','suspended') THEN
        RAISE EXCEPTION 'invalid tier: %', p_tier;
    END IF;
    SELECT tier, default_fee_formulas_id INTO v_old, v_current_formula
      FROM tenants WHERE tenants_id = p_tenants_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'tenant not found: %', p_tenants_id;
    END IF;

    UPDATE tenants SET tier = p_tier, updated_at = now() WHERE tenants_id = p_tenants_id;

    IF p_tier <> 'suspended' THEN
        SELECT name INTO v_current_name FROM fee_formulas WHERE fee_formulas_id = v_current_formula;
        -- Only replace tier-managed (or missing) formulas; custom overrides stay.
        IF v_current_formula IS NULL OR v_current_name LIKE 'tier:%' THEN
            SELECT * INTO t FROM app.tier_pricing(p_tier);
            v_formula := app.ensure_tier_formula('tier:' || p_tier, t.percent_bps, t.flat_cents, t.min_fee_cents);
            UPDATE tenants SET default_fee_formulas_id = v_formula, updated_at = now()
             WHERE tenants_id = p_tenants_id;
        END IF;
    END IF;
    RETURN v_old;
END; $$;
