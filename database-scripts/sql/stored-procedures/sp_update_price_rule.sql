DROP FUNCTION IF EXISTS sp_update_price_rule(uuid, text, text, int, int, timestamptz, timestamptz, int, int, bool);

CREATE OR REPLACE FUNCTION sp_update_price_rule(
    p_price_rules_id uuid, p_name text, p_rule_type text, p_priority int,
    p_price_cents int, p_active_from timestamptz, p_active_until timestamptz,
    p_min_remaining int, p_max_remaining int, p_is_active bool, p_capacity int DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE price_rules SET
        name = p_name, rule_type = p_rule_type, priority = COALESCE(p_priority, 0),
        price_cents = p_price_cents, active_from = p_active_from,
        active_until = p_active_until, min_remaining = p_min_remaining,
        max_remaining = p_max_remaining, is_active = COALESCE(p_is_active, true),
        capacity = p_capacity,
        updated_at = now()
    WHERE price_rules_id = p_price_rules_id;
END; $$;
