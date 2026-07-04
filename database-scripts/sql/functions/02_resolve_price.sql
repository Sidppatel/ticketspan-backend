-- Pricing Module core resolution. The SINGLE server-authoritative source of truth
-- for "what does this priceable cost right now and how does the money split".
-- Built on app.compute_fee. Every consumer — admin preview (sp_calculate_price),
-- customer checkout quote, and the booking-write SPs — calls app.price_breakdown.
-- No pricing math lives anywhere else.

-- Resolves the effective fee formula for a price: the price's explicit override
-- when set (developer-only), otherwise the owning tenant's default fee formula.
CREATE OR REPLACE FUNCTION app.resolve_fee_formula(p_explicit uuid, p_tenant uuid)
RETURNS uuid
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT COALESCE(
        p_explicit,
        (SELECT default_fee_formulas_id FROM tenants WHERE tenants_id = p_tenant)
    );
$$;

-- Event-aware resolution: explicit price formula > active (non-expired)
-- event-level override (Pay Per Event / manual event fee override) > tenant
-- default. All checkout math routes through this overload.
CREATE OR REPLACE FUNCTION app.resolve_fee_formula(p_explicit uuid, p_event uuid, p_tenant uuid)
RETURNS uuid
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT COALESCE(
        p_explicit,
        (SELECT e.fee_formulas_id FROM events e
          WHERE e.events_id = p_event
            AND e.fee_formulas_id IS NOT NULL
            AND (e.fee_override_expires_at IS NULL OR e.fee_override_expires_at > now())),
        (SELECT default_fee_formulas_id FROM tenants WHERE tenants_id = p_tenant)
    );
$$;

-- Resolves the gateway (payment-processing) fee formula for a tenant. NULL when
-- the tenant has none configured, which app.compute_fee treats as a zero fee.
CREATE OR REPLACE FUNCTION app.resolve_gateway_formula(p_tenant uuid)
RETURNS uuid
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT gateway_fee_formulas_id FROM tenants WHERE tenants_id = p_tenant;
$$;

-- Resolves the ACH fee formula for a tenant: the flat fee that replaces the
-- service fee when the buyer pays by ACH. NULL = no ACH fee (compute_fee → 0).
CREATE OR REPLACE FUNCTION app.resolve_ach_formula(p_tenant uuid)
RETURNS uuid
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT ach_fee_formulas_id FROM tenants WHERE tenants_id = p_tenant;
$$;

-- Resolve the active price for a sellable item, applying prioritized price rules,
-- and computing server-authoritative fees.
--
-- Fee structures:
--   1. platform fee = resolved fee_formulas (e.g. 6% + $1.50) applied to the selling price
--   2. gateway fee  = fixed processing fee (e.g. 2.9% + $0.30) of total charged amount (selling + platform)
--
-- Returns a complete breakdown snapshot.

-- Method-aware overload. p_method 'card' (default) = today's behavior: service fee
-- + gateway fee. p_method 'ach' = the service fee is REPLACED by the tenant's flat
-- ACH fee (and gateway is suppressed), so the buyer's total drops to selling + ACH.
CREATE OR REPLACE FUNCTION app.price_breakdown_for_method(
    p_prices_id uuid, p_at timestamptz, p_seats int, p_remaining int, p_method text
)
RETURNS TABLE(
    base_price_cents int,
    selling_price_cents int,
    discount_cents int,
    applied_price_rules_id uuid,
    applied_rule_name text,
    platform_fee_cents int,
    gateway_fee_cents int,
    tax_cents int,
    final_price_cents int,
    organizer_net_cents int,
    currency text
)
LANGUAGE plpgsql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_tenant uuid; v_event uuid; v_type text; v_base int; v_per int; v_allinc bool; v_explicit uuid;
    v_formula uuid; v_gw_formula uuid;
    v_seats int := GREATEST(COALESCE(p_seats, 1), 1);
    v_rule_id uuid; v_rule_name text; v_rule_price int;
    v_base_unit int; v_sell_unit int;
    v_base_sub int; v_sell_sub int;
    v_platform int; v_gateway int;
BEGIN
    -- Alias every source column: the RETURNS TABLE OUT params share names with prices columns
    SELECT pp.tenants_id, pp.events_id, pp.pricing_type, pp.base_price_cents, pp.per_attendee_cents,
           pp.is_all_inclusive, pp.fee_formulas_id
      INTO v_tenant, v_event, v_type, v_base, v_per, v_allinc, v_explicit
      FROM prices pp
     WHERE pp.prices_id = p_prices_id AND pp.is_active = true;
    IF NOT FOUND THEN
        RETURN;
    END IF;

    -- ACH replaces the service fee with the tenant's flat ACH fee and drops the
    -- gateway leg; card keeps the resolved service fee + gateway fee.
    IF p_method = 'ach' THEN
        v_formula := app.resolve_ach_formula(v_tenant);
        v_gw_formula := NULL;
    ELSE
        v_formula := app.resolve_fee_formula(v_explicit, v_event, v_tenant);
        v_gw_formula := app.resolve_gateway_formula(v_tenant);
    END IF;

    -- Per-item wins: a matching rule scoped to THIS price beats an event-wide rule
    SELECT pr.price_rules_id, pr.name, pr.price_cents
      INTO v_rule_id, v_rule_name, v_rule_price
      FROM price_rules pr
     WHERE pr.scope = 'Price'
       AND pr.prices_id = p_prices_id
       AND pr.is_active = true
       AND (pr.active_from IS NULL OR pr.active_from <= p_at)
       AND (pr.active_until IS NULL OR pr.active_until > p_at)
       AND (pr.min_remaining IS NULL OR p_remaining IS NULL OR p_remaining >= pr.min_remaining)
       AND (pr.max_remaining IS NULL OR p_remaining IS NULL OR p_remaining <= pr.max_remaining)
     ORDER BY pr.priority DESC, pr.created_at ASC
     LIMIT 1;

    IF v_rule_id IS NULL THEN
        SELECT pr.price_rules_id, pr.name, pr.price_cents
          INTO v_rule_id, v_rule_name, v_rule_price
          FROM price_rules pr
         WHERE pr.scope = 'Event'
           AND pr.events_id = v_event
           AND pr.is_active = true
           AND (pr.active_from IS NULL OR pr.active_from <= p_at)
           AND (pr.active_until IS NULL OR pr.active_until > p_at)
           AND (pr.min_remaining IS NULL OR p_remaining IS NULL OR p_remaining >= pr.min_remaining)
           AND (pr.max_remaining IS NULL OR p_remaining IS NULL OR p_remaining <= pr.max_remaining)
         ORDER BY pr.priority DESC, pr.created_at ASC
         LIMIT 1;
    END IF;

    v_base_unit := v_base;
    v_sell_unit := COALESCE(v_rule_price, v_base);

    IF v_type = 'Table' THEN
        IF v_allinc THEN
            v_base_sub := v_base_unit;
            v_sell_sub := v_sell_unit;
        ELSE
            v_base_sub := v_base_unit + v_per * v_seats;
            v_sell_sub := v_sell_unit + v_per * v_seats;
        END IF;
        v_platform := app.compute_fee(v_sell_sub, v_formula);
    ELSE
        v_base_sub := v_base_unit * v_seats;
        v_sell_sub := v_sell_unit * v_seats;
        v_platform := app.compute_fee(v_sell_unit, v_formula) * v_seats;
    END IF;

    v_gateway := app.compute_fee(v_sell_sub + v_platform, v_gw_formula);

    base_price_cents := v_base_sub;
    selling_price_cents := v_sell_sub;
    discount_cents := GREATEST(v_base_sub - v_sell_sub, 0);
    applied_price_rules_id := v_rule_id;
    applied_rule_name := v_rule_name;
    platform_fee_cents := v_platform;
    gateway_fee_cents := v_gateway;
    tax_cents := 0;
    final_price_cents := v_sell_sub + v_platform + v_gateway;
    organizer_net_cents := v_sell_sub;
    currency := 'usd';
    RETURN NEXT;
END; $$;

-- Default (card) breakdown: unchanged 4-arg signature every existing caller uses.
CREATE OR REPLACE FUNCTION app.price_breakdown(
    p_prices_id uuid, p_at timestamptz, p_seats int, p_remaining int
)
RETURNS TABLE(
    base_price_cents int,
    selling_price_cents int,
    discount_cents int,
    applied_price_rules_id uuid,
    applied_rule_name text,
    platform_fee_cents int,
    gateway_fee_cents int,
    tax_cents int,
    final_price_cents int,
    organizer_net_cents int,
    currency text
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM app.price_breakdown_for_method(p_prices_id, p_at, p_seats, p_remaining, 'card');
$$;

-- Back-compat thin wrapper: legacy callers that only need subtotal/fee/total.
-- subtotal = selling price, fee = platform + gateway, total = final.
CREATE OR REPLACE FUNCTION app.resolve_price(
    p_prices_id uuid, p_at timestamptz, p_seats int, p_remaining int
)
RETURNS TABLE(subtotal_cents int, fee_cents int, total_cents int)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT selling_price_cents,
           platform_fee_cents + gateway_fee_cents + tax_cents,
           final_price_cents
    FROM app.price_breakdown(p_prices_id, p_at, p_seats, p_remaining);
$$;
