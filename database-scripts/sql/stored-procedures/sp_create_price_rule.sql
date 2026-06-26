CREATE OR REPLACE FUNCTION sp_create_price_rule(
    p_prices_id uuid, p_name text, p_rule_type text, p_priority int, p_price_cents int,
    p_active_from timestamptz, p_active_until timestamptz,
    p_min_remaining int, p_max_remaining int
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO price_rules (tenants_id, prices_id, name, rule_type, priority,
        price_cents, active_from, active_until, min_remaining, max_remaining,
        is_active, created_at, updated_at)
    VALUES ((SELECT tenants_id FROM prices WHERE prices_id = p_prices_id),
        p_prices_id, p_name, p_rule_type, COALESCE(p_priority, 0), p_price_cents,
        p_active_from, p_active_until, p_min_remaining, p_max_remaining,
        true, now(), now())
    RETURNING price_rules_id INTO v_id;
    RETURN v_id;
END; $$;
