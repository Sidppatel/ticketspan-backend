
DROP VIEW IF EXISTS vw_event_ticket_types_pricing CASCADE;

DROP FUNCTION IF EXISTS app.price_breakdown_for_method(uuid, timestamptz, int, int, text);
DROP FUNCTION IF EXISTS app.price_breakdown_for_method(uuid, timestamptz, int, int, text, int, int);
DROP FUNCTION IF EXISTS app.price_breakdown(uuid, timestamptz, int, int);
DROP FUNCTION IF EXISTS app.price_breakdown(uuid, timestamptz, int, int, int, int);

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

CREATE OR REPLACE FUNCTION app.resolve_gateway_formula(p_tenant uuid)
RETURNS uuid
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT gateway_fee_formulas_id FROM tenants WHERE tenants_id = p_tenant;
$$;

CREATE OR REPLACE FUNCTION app.resolve_ach_formula(p_tenant uuid)
RETURNS uuid
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT ach_fee_formulas_id FROM tenants WHERE tenants_id = p_tenant;
$$;

CREATE OR REPLACE FUNCTION app.resolve_group_rule(
    p_prices_id uuid, p_event uuid, p_qty_price int, p_qty_event int, p_at timestamptz
)
RETURNS TABLE(
    price_rules_id uuid,
    name text,
    discount_kind text,
    price_cents int,
    discount_bps int,
    capacity int
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT pr.price_rules_id, pr.name, pr.discount_kind, pr.price_cents, pr.discount_bps, pr.capacity
      FROM price_rules pr
     WHERE pr.rule_type = 'Group'
       AND pr.is_active = true
       AND pr.discount_kind IN ('FixedUnitPrice', 'PercentOff')
       AND (pr.active_from IS NULL OR pr.active_from <= p_at)
       AND (pr.active_until IS NULL OR pr.active_until > p_at)
       AND (
             (pr.scope = 'Price' AND pr.prices_id = p_prices_id
              AND COALESCE(p_qty_price, 0) >= pr.min_qty
              AND (pr.max_qty IS NULL OR COALESCE(p_qty_price, 0) <= pr.max_qty))
          OR (pr.scope = 'Event' AND pr.events_id = p_event
              AND COALESCE(p_qty_event, 0) >= pr.min_qty
              AND (pr.max_qty IS NULL OR COALESCE(p_qty_event, 0) <= pr.max_qty))
           )
     ORDER BY (pr.scope = 'Price') DESC, pr.min_qty DESC, pr.priority DESC, pr.created_at ASC
     LIMIT 1;
$$;

CREATE OR REPLACE FUNCTION app.group_order_discount(
    p_event uuid, p_qty_event int, p_subtotal_cents int, p_at timestamptz
)
RETURNS TABLE(
    price_rules_id uuid,
    name text,
    amount_cents int
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT pr.price_rules_id, pr.name,
           LEAST(GREATEST(pr.price_cents, 0), GREATEST(COALESCE(p_subtotal_cents, 0), 0))
      FROM price_rules pr
     WHERE pr.rule_type = 'Group'
       AND pr.is_active = true
       AND pr.discount_kind = 'AmountOffOrder'
       AND pr.scope = 'Event'
       AND pr.events_id = p_event
       AND (pr.active_from IS NULL OR pr.active_from <= p_at)
       AND (pr.active_until IS NULL OR pr.active_until > p_at)
       AND COALESCE(p_qty_event, 0) >= pr.min_qty
       AND (pr.max_qty IS NULL OR COALESCE(p_qty_event, 0) <= pr.max_qty)
     ORDER BY pr.min_qty DESC, pr.priority DESC, pr.created_at ASC
     LIMIT 1;
$$;

CREATE OR REPLACE FUNCTION app.price_breakdown_for_method(
    p_prices_id uuid, p_at timestamptz, p_seats int, p_remaining int, p_method text,
    p_qty_price int DEFAULT 0, p_qty_event int DEFAULT 0
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
    currency text,
    group_discounted_seats int,
    group_unit_cents int,
    standard_unit_cents int
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
    v_tax_rate numeric; v_tax int;
    v_grp record; v_group_unit int := 0; v_disc_seats int := 0;
BEGIN
    SELECT pp.tenants_id, pp.events_id, pp.pricing_type, pp.base_price_cents, pp.per_attendee_cents,
           pp.is_all_inclusive, pp.fee_formulas_id
      INTO v_tenant, v_event, v_type, v_base, v_per, v_allinc, v_explicit
      FROM prices pp
     WHERE pp.prices_id = p_prices_id AND pp.is_active = true;
    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF p_method = 'ach' THEN
        v_formula := app.resolve_ach_formula(v_tenant);
        v_gw_formula := NULL;
    ELSE
        v_formula := app.resolve_fee_formula(v_explicit, v_event, v_tenant);
        v_gw_formula := app.resolve_gateway_formula(v_tenant);
    END IF;

    SELECT pr.price_rules_id, pr.name, pr.price_cents
      INTO v_rule_id, v_rule_name, v_rule_price
      FROM price_rules pr
     WHERE pr.scope = 'Price'
       AND pr.prices_id = p_prices_id
       AND pr.is_active = true
       AND pr.rule_type <> 'Group'
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
           AND pr.rule_type <> 'Group'
           AND (pr.active_from IS NULL OR pr.active_from <= p_at)
           AND (pr.active_until IS NULL OR pr.active_until > p_at)
           AND (pr.min_remaining IS NULL OR p_remaining IS NULL OR p_remaining >= pr.min_remaining)
           AND (pr.max_remaining IS NULL OR p_remaining IS NULL OR p_remaining <= pr.max_remaining)
         ORDER BY pr.priority DESC, pr.created_at ASC
         LIMIT 1;
    END IF;

    v_base_unit := v_base;
    v_sell_unit := COALESCE(v_rule_price, v_base);

    IF v_type <> 'Table' THEN
        SELECT * INTO v_grp FROM app.resolve_group_rule(p_prices_id, v_event, p_qty_price, p_qty_event, p_at);
        IF v_grp.price_rules_id IS NOT NULL THEN
            IF v_grp.discount_kind = 'PercentOff' THEN
                v_group_unit := GREATEST(
                    v_base_unit - round(v_base_unit * COALESCE(v_grp.discount_bps, 0) / 10000.0)::int, 0);
            ELSE
                v_group_unit := GREATEST(COALESCE(v_grp.price_cents, v_base_unit), 0);
            END IF;
            IF v_group_unit < v_sell_unit THEN
                v_disc_seats := LEAST(v_seats, COALESCE(v_grp.capacity, v_seats));
            END IF;
        END IF;
    END IF;

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

        v_sell_sub := v_group_unit * v_disc_seats + v_sell_unit * (v_seats - v_disc_seats);
        IF v_disc_seats > 0 THEN
            v_rule_id := v_grp.price_rules_id;
            v_rule_name := v_grp.name;
        END IF;
        v_platform := app.compute_fee(v_sell_sub, v_formula);
    END IF;

    v_gateway := app.compute_fee(v_sell_sub + v_platform, v_gw_formula);

    v_tax_rate := app.event_tax_rate(v_event);
    IF v_sell_sub > 0 AND v_tax_rate > 0 THEN
        v_tax := round((v_sell_sub + v_platform + v_gateway) * v_tax_rate)::int;
    ELSE
        v_tax := 0;
    END IF;

    base_price_cents := v_base_sub;
    selling_price_cents := v_sell_sub;
    discount_cents := GREATEST(v_base_sub - v_sell_sub, 0);
    applied_price_rules_id := v_rule_id;
    applied_rule_name := v_rule_name;
    platform_fee_cents := v_platform;
    gateway_fee_cents := v_gateway;
    tax_cents := v_tax;
    final_price_cents := v_sell_sub + v_platform + v_gateway + v_tax;
    organizer_net_cents := v_sell_sub;
    currency := 'usd';
    group_discounted_seats := v_disc_seats;
    group_unit_cents := CASE WHEN v_disc_seats > 0 THEN v_group_unit ELSE 0 END;
    standard_unit_cents := v_sell_unit;
    RETURN NEXT;
END; $$;

CREATE OR REPLACE FUNCTION app.order_fees(
    p_event_id uuid, p_subtotal_cents int, p_method text DEFAULT 'card'
)
RETURNS TABLE(platform_fee_cents int, gateway_fee_cents int)
LANGUAGE plpgsql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_tenant uuid; v_formula uuid; v_gw_formula uuid; v_platform int;
BEGIN
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_event_id;

    IF p_method = 'ach' THEN
        v_formula := app.resolve_ach_formula(v_tenant);
        v_gw_formula := NULL;
    ELSE

        v_formula := app.resolve_fee_formula(NULL, p_event_id, v_tenant);
        v_gw_formula := app.resolve_gateway_formula(v_tenant);
    END IF;

    v_platform := app.compute_fee(COALESCE(p_subtotal_cents, 0), v_formula);
    platform_fee_cents := v_platform;
    gateway_fee_cents := app.compute_fee(COALESCE(p_subtotal_cents, 0) + v_platform, v_gw_formula);
    RETURN NEXT;
END; $$;

CREATE OR REPLACE FUNCTION app.price_breakdown(
    p_prices_id uuid, p_at timestamptz, p_seats int, p_remaining int,
    p_qty_price int DEFAULT 0, p_qty_event int DEFAULT 0
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
    currency text,
    group_discounted_seats int,
    group_unit_cents int,
    standard_unit_cents int
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM app.price_breakdown_for_method(p_prices_id, p_at, p_seats, p_remaining, 'card',
                      p_qty_price, p_qty_event);
$$;
