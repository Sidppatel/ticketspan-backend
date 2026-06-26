-- Pricing Module core resolution functions. Single server-authoritative source
-- of truth for "what does this priceable cost right now". Built on app.compute_fee.

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

-- Calculates the final price for a single line of a given price entity at a point
-- in time, for a seat count, given remaining inventory. Applies the
-- highest-priority active price_rule whose time window and inventory conditions
-- match (presale / last-minute / dynamic), then table per-attendee math, then the
-- resolved fee formula. Returns the whole-line subtotal/fee/total.
--   Table, all-inclusive : subtotal = resolved base
--   Table, per-attendee  : subtotal = resolved base + per_attendee_cents * seats
--   TicketTier / AddOn    : subtotal = resolved unit * seats (fee per seat)
CREATE OR REPLACE FUNCTION app.resolve_price(
    p_prices_id uuid, p_at timestamptz, p_seats int, p_remaining int
)
RETURNS TABLE(subtotal_cents int, fee_cents int, total_cents int)
LANGUAGE plpgsql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_tenant uuid; v_event uuid; v_type text; v_base int; v_per int; v_allinc bool; v_explicit uuid;
    v_formula uuid; v_seats int := GREATEST(COALESCE(p_seats, 1), 1);
    v_rule_price int; v_unit int; v_sub int; v_fee int;
BEGIN
    SELECT tenants_id, events_id, pricing_type, base_price_cents, per_attendee_cents,
           is_all_inclusive, fee_formulas_id
      INTO v_tenant, v_event, v_type, v_base, v_per, v_allinc, v_explicit
      FROM prices
     WHERE prices_id = p_prices_id AND is_active = true;
    IF NOT FOUND THEN
        RETURN;
    END IF;

    v_formula := app.resolve_fee_formula(v_explicit, v_tenant);

    -- Per-item wins: prefer a matching rule scoped to THIS price; only if none
    -- matches fall back to an event-wide rule covering the price's event. Within
    -- each scope the highest priority (then oldest) rule applies.
    SELECT pr.price_cents INTO v_rule_price
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

    IF v_rule_price IS NULL THEN
        SELECT pr.price_cents INTO v_rule_price
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

    v_unit := COALESCE(v_rule_price, v_base);

    IF v_type = 'Table' THEN
        IF v_allinc THEN
            v_sub := v_unit;
        ELSE
            v_sub := v_unit + v_per * v_seats;
        END IF;
        v_fee := app.compute_fee(v_sub, v_formula);
    ELSE
        v_sub := v_unit * v_seats;
        v_fee := app.compute_fee(v_unit, v_formula) * v_seats;
    END IF;

    subtotal_cents := v_sub;
    fee_cents := v_fee;
    total_cents := v_sub + v_fee;
    RETURN NEXT;
END; $$;
