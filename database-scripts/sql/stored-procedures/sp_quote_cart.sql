DROP FUNCTION IF EXISTS sp_quote_cart(uuid, jsonb);

CREATE OR REPLACE FUNCTION sp_quote_cart(p_event_id uuid, p_lines jsonb)
RETURNS TABLE(
    kind text, ref_id uuid, label text, seats int,
    base_price_cents int, selling_price_cents int, discount_cents int,
    applied_price_rules_id uuid, applied_rule_name text,
    platform_fee_cents int, gateway_fee_cents int, tax_cents int,
    final_price_cents int, organizer_net_cents int, currency text,
    ach_available boolean, ach_final_cents int,
    group_discounted_seats int, group_unit_cents int, standard_unit_cents int
)
LANGUAGE plpgsql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_line jsonb; v_kind text; v_ref uuid; v_seats int;
    v_prices_id uuid; v_label text; v_cap int;
    v_bd record; v_ach_bd record; v_ach_ok boolean; v_ach_final int;
    v_qty_event int := 0; v_qty_price int;
    v_qty_by_price jsonb := '{}'::jsonb;
    v_priced jsonb := '[]'::jsonb;
    v_ticket_subtotal int := 0;
    v_order record; v_order_amount int := 0; v_allocated int := 0;
    v_idx int := 0; v_max_idx int := 0; v_max_selling int := -1;
    v_alloc int; v_selling int; v_has_unit_group boolean := false;
BEGIN
    IF p_lines IS NULL OR jsonb_typeof(p_lines) <> 'array' THEN
        RETURN;
    END IF;

    SELECT COALESCE(t.ach_enabled AND e.ach_enabled, false) INTO v_ach_ok
      FROM events e JOIN tenants t ON t.tenants_id = e.tenants_id
     WHERE e.events_id = p_event_id;

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

    FOR v_line IN SELECT * FROM jsonb_array_elements(p_lines) LOOP
        v_kind := v_line->>'kind';
        v_ref := (v_line->>'ref_id')::uuid;
        v_seats := GREATEST(COALESCE((v_line->>'seats')::int, 1), 1);

        IF v_kind = 'Ticket' THEN
            SELECT ett.prices_id, ett.label INTO v_prices_id, v_label
              FROM event_ticket_types ett WHERE ett.event_ticket_types_id = v_ref;
        ELSIF v_kind = 'Table' THEN
            SELECT et.prices_id, t.label, COALESCE(t.capacity_override, et.capacity)
              INTO v_prices_id, v_label, v_cap
              FROM tables t JOIN event_tables et ON et.event_tables_id = t.event_tables_id
             WHERE t.tables_id = v_ref;
            v_seats := GREATEST(COALESCE(v_cap, 1), 1);
        ELSE
            CONTINUE;
        END IF;

        IF v_prices_id IS NULL THEN
            CONTINUE;
        END IF;

        IF v_kind = 'Ticket' THEN
            v_qty_price := COALESCE((v_qty_by_price->>v_prices_id::text)::int, 0);
        ELSE
            v_qty_price := 0;
        END IF;

        SELECT * INTO v_bd FROM app.price_breakdown(v_prices_id, now(), v_seats,
                                 app.remaining_for_price(v_prices_id),
                                 v_qty_price,
                                 CASE WHEN v_kind = 'Ticket' THEN v_qty_event ELSE 0 END);

        IF v_ach_ok THEN
            SELECT * INTO v_ach_bd FROM app.price_breakdown_for_method(v_prices_id, now(), v_seats,
                                         app.remaining_for_price(v_prices_id), 'ach',
                                         v_qty_price,
                                         CASE WHEN v_kind = 'Ticket' THEN v_qty_event ELSE 0 END);
            v_ach_final := v_ach_bd.final_price_cents;
        ELSE
            v_ach_final := v_bd.final_price_cents;
        END IF;

        IF v_kind = 'Ticket' THEN
            v_ticket_subtotal := v_ticket_subtotal + v_bd.selling_price_cents;
            IF COALESCE(v_bd.group_discounted_seats, 0) > 0 THEN
                v_has_unit_group := true;
            END IF;
        END IF;

        v_priced := v_priced || jsonb_build_object(
            'kind', v_kind, 'ref_id', v_ref::text, 'label', COALESCE(v_label, ''), 'seats', v_seats,
            'base', v_bd.base_price_cents, 'selling', v_bd.selling_price_cents,
            'discount', v_bd.discount_cents, 'rule_id', v_bd.applied_price_rules_id,
            'rule_name', v_bd.applied_rule_name, 'platform', v_bd.platform_fee_cents,
            'gateway', v_bd.gateway_fee_cents, 'tax', v_bd.tax_cents,
            'final', v_bd.final_price_cents, 'net', v_bd.organizer_net_cents,
            'currency', v_bd.currency,
            'ach_final', v_ach_final,
            'grp_seats', v_bd.group_discounted_seats, 'grp_unit', v_bd.group_unit_cents,
            'std_unit', v_bd.standard_unit_cents);
    END LOOP;

    IF NOT v_has_unit_group THEN
        SELECT * INTO v_order FROM app.group_order_discount(p_event_id, v_qty_event, v_ticket_subtotal, now());
        v_order_amount := COALESCE(v_order.amount_cents, 0);
    END IF;

    IF v_order_amount > 0 AND v_ticket_subtotal > 0 THEN
        FOR v_line IN SELECT * FROM jsonb_array_elements(v_priced) LOOP
            v_idx := v_idx + 1;
            IF v_line->>'kind' = 'Ticket' THEN
                v_selling := (v_line->>'selling')::int;
                IF v_selling > v_max_selling THEN
                    v_max_selling := v_selling;
                    v_max_idx := v_idx;
                END IF;
                v_allocated := v_allocated + (v_order_amount * v_selling) / v_ticket_subtotal;
            END IF;
        END LOOP;
    END IF;

    v_idx := 0;
    FOR v_line IN SELECT * FROM jsonb_array_elements(v_priced) LOOP
        v_idx := v_idx + 1;
        kind := v_line->>'kind';
        ref_id := (v_line->>'ref_id')::uuid;
        label := v_line->>'label';
        seats := (v_line->>'seats')::int;
        base_price_cents := (v_line->>'base')::int;
        selling_price_cents := (v_line->>'selling')::int;
        discount_cents := (v_line->>'discount')::int;
        applied_price_rules_id := NULLIF(v_line->>'rule_id', '')::uuid;
        applied_rule_name := NULLIF(v_line->>'rule_name', '');
        currency := v_line->>'currency';
        group_discounted_seats := (v_line->>'grp_seats')::int;
        group_unit_cents := (v_line->>'grp_unit')::int;
        standard_unit_cents := (v_line->>'std_unit')::int;

        IF v_order_amount > 0 AND v_ticket_subtotal > 0 AND kind = 'Ticket' THEN
            v_alloc := (v_order_amount * selling_price_cents) / v_ticket_subtotal;
            IF v_idx = v_max_idx THEN
                v_alloc := v_alloc + (v_order_amount - v_allocated);
            END IF;
            v_alloc := LEAST(v_alloc, selling_price_cents);
            selling_price_cents := selling_price_cents - v_alloc;
            discount_cents := discount_cents + v_alloc;
            applied_price_rules_id := v_order.price_rules_id;
            applied_rule_name := v_order.name;
            SELECT ofees.platform_fee_cents, ofees.gateway_fee_cents
              INTO platform_fee_cents, gateway_fee_cents
              FROM app.order_fees(p_event_id, selling_price_cents, 'card') ofees;
            tax_cents := COALESCE(
                round((selling_price_cents + platform_fee_cents + gateway_fee_cents)
                      * COALESCE(app.event_tax_rate(p_event_id), 0))::int, 0);
            final_price_cents := selling_price_cents + platform_fee_cents + gateway_fee_cents + tax_cents;
            organizer_net_cents := selling_price_cents;
            ach_final_cents := final_price_cents;
        ELSE
            platform_fee_cents := (v_line->>'platform')::int;
            gateway_fee_cents := (v_line->>'gateway')::int;
            tax_cents := (v_line->>'tax')::int;
            final_price_cents := (v_line->>'final')::int;
            organizer_net_cents := (v_line->>'net')::int;
            ach_final_cents := (v_line->>'ach_final')::int;
        END IF;

        ach_available := v_ach_ok;
        RETURN NEXT;
    END LOOP;
END; $$;
