DROP FUNCTION IF EXISTS sp_group_discount_hint(uuid, jsonb);

CREATE OR REPLACE FUNCTION sp_group_discount_hint(p_event_id uuid, p_lines jsonb)
RETURNS TABLE(
    applied_rule_name text,
    applied_min_qty int,
    group_discount_cents int,
    discounted_seats int,
    capped boolean,
    eligible_qty int,
    next_tier_min_qty int,
    next_tier_seats_away int,
    next_tier_kind text,
    next_tier_bps int,
    next_tier_price_cents int
)
LANGUAGE plpgsql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_line jsonb; v_seats int; v_prices_id uuid;
    v_qty_event int := 0;
    v_qty_by_price jsonb := '{}'::jsonb;
    v_next record;
BEGIN
    applied_rule_name := NULL; applied_min_qty := 0; group_discount_cents := 0;
    discounted_seats := 0; capped := false; eligible_qty := 0;
    next_tier_min_qty := 0; next_tier_seats_away := 0;
    next_tier_kind := NULL; next_tier_bps := 0; next_tier_price_cents := 0;

    IF p_lines IS NULL OR jsonb_typeof(p_lines) <> 'array' THEN
        RETURN NEXT;
        RETURN;
    END IF;

    FOR v_line IN SELECT * FROM jsonb_array_elements(p_lines) LOOP
        IF v_line->>'kind' <> 'Ticket' THEN
            CONTINUE;
        END IF;
        v_seats := GREATEST(COALESCE((v_line->>'seats')::int, 1), 1);
        SELECT ett.prices_id INTO v_prices_id
          FROM event_ticket_types ett WHERE ett.event_ticket_types_id = (v_line->>'ref_id')::uuid;
        IF v_prices_id IS NULL THEN
            CONTINUE;
        END IF;
        v_qty_event := v_qty_event + v_seats;
        v_qty_by_price := jsonb_set(v_qty_by_price, ARRAY[v_prices_id::text],
            to_jsonb(COALESCE((v_qty_by_price->>v_prices_id::text)::int, 0) + v_seats));
    END LOOP;

    eligible_qty := v_qty_event;

    SELECT COALESCE(SUM(q.base_price_cents - q.selling_price_cents), 0)::int,
           COALESCE(SUM(q.group_discounted_seats), 0)::int,
           COALESCE(BOOL_OR(q.group_discounted_seats > 0 AND q.group_discounted_seats < q.seats), false),
           MAX(pr.name), COALESCE(MAX(pr.min_qty), 0)
      INTO group_discount_cents, discounted_seats, capped, applied_rule_name, applied_min_qty
      FROM sp_quote_cart(p_event_id, p_lines) q
      JOIN price_rules pr ON pr.price_rules_id = q.applied_price_rules_id
     WHERE pr.rule_type = 'Group';

    SELECT pr.min_qty, pr.discount_kind, pr.discount_bps, pr.price_cents,
           (pr.min_qty - CASE WHEN pr.scope = 'Price'
                              THEN COALESCE((v_qty_by_price->>pr.prices_id::text)::int, 0)
                              ELSE v_qty_event END) AS seats_away
      INTO v_next
      FROM price_rules pr
     WHERE pr.rule_type = 'Group'
       AND pr.is_active = true
       AND (pr.active_from IS NULL OR pr.active_from <= now())
       AND (pr.active_until IS NULL OR pr.active_until > now())
       AND (
             (pr.scope = 'Event' AND pr.events_id = p_event_id
              AND pr.min_qty > v_qty_event)
          OR (pr.scope = 'Price'
              AND pr.prices_id IN (SELECT pp.prices_id FROM prices pp
                                    WHERE pp.events_id = p_event_id AND pp.is_active)
              AND pr.min_qty > COALESCE((v_qty_by_price->>pr.prices_id::text)::int, 0))
           )
     ORDER BY pr.min_qty ASC
     LIMIT 1;

    IF v_next.min_qty IS NOT NULL THEN
        next_tier_min_qty := v_next.min_qty;
        next_tier_kind := v_next.discount_kind;
        next_tier_bps := COALESCE(v_next.discount_bps, 0);
        next_tier_price_cents := COALESCE(v_next.price_cents, 0);
        next_tier_seats_away := GREATEST(v_next.seats_away, 0);
    END IF;

    RETURN NEXT;
END; $$;
