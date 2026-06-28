DROP FUNCTION IF EXISTS sp_create_price_rule(uuid, text, text, int, int, timestamptz, timestamptz, int, int);
DROP FUNCTION IF EXISTS sp_create_price_rule(uuid, text, text, int, int, timestamptz, timestamptz, int, int, text);
DROP FUNCTION IF EXISTS sp_create_price_rule(uuid, text, text, int, int, timestamptz, timestamptz, int, int, int, text);

-- Creates a price rule at one of two scopes:
--   scope='Price' : targets a single price (tier / table type) via p_owner_id.
--   scope='Event' : targets every price of an event via p_owner_id (events_id).
-- The polymorphic p_owner_id keeps one entry point for both; tenants_id is derived
-- from whichever owner is referenced.
CREATE OR REPLACE FUNCTION sp_create_price_rule(
    p_owner_id uuid, p_name text, p_rule_type text, p_priority int, p_price_cents int,
    p_active_from timestamptz, p_active_until timestamptz,
    p_min_remaining int, p_max_remaining int,
    p_capacity int DEFAULT NULL, p_scope text DEFAULT 'Price'
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid; v_scope text := COALESCE(NULLIF(p_scope, ''), 'Price');
BEGIN
    IF v_scope = 'Event' THEN
        INSERT INTO price_rules (tenants_id, scope, events_id, prices_id, name, rule_type,
            priority, price_cents, active_from, active_until, min_remaining, max_remaining,
            capacity, is_active, created_at, updated_at)
        VALUES ((SELECT tenants_id FROM events WHERE events_id = p_owner_id),
            'Event', p_owner_id, NULL, p_name, p_rule_type, COALESCE(p_priority, 0), p_price_cents,
            p_active_from, p_active_until, p_min_remaining, p_max_remaining,
            p_capacity, true, now(), now())
        RETURNING price_rules_id INTO v_id;
    ELSE
        INSERT INTO price_rules (tenants_id, scope, events_id, prices_id, name, rule_type,
            priority, price_cents, active_from, active_until, min_remaining, max_remaining,
            capacity, is_active, created_at, updated_at)
        VALUES ((SELECT tenants_id FROM prices WHERE prices_id = p_owner_id),
            'Price', NULL, p_owner_id, p_name, p_rule_type, COALESCE(p_priority, 0), p_price_cents,
            p_active_from, p_active_until, p_min_remaining, p_max_remaining,
            p_capacity, true, now(), now())
        RETURNING price_rules_id INTO v_id;
    END IF;
    RETURN v_id;
END; $$;
