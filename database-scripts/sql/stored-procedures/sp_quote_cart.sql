DROP FUNCTION IF EXISTS sp_quote_cart(uuid, jsonb);

-- Non-reserving cart preview. Resolves each requested line to its full pricing
-- breakdown via app.price_breakdown (the same engine the checkout write uses), with
-- NO locking and NO writes. Powers the customer checkout summary and the admin
-- multi-item preview so both render exactly what checkout will charge.
--   p_lines: jsonb array of {"kind":"Ticket"|"Table","ref_id":uuid,"seats":int}
CREATE OR REPLACE FUNCTION sp_quote_cart(p_event_id uuid, p_lines jsonb)
RETURNS TABLE(
    kind text, ref_id uuid, label text, seats int,
    base_price_cents int, selling_price_cents int, discount_cents int,
    applied_price_rules_id uuid, applied_rule_name text,
    platform_fee_cents int, gateway_fee_cents int, tax_cents int,
    final_price_cents int, organizer_net_cents int, currency text,
    ach_available boolean, ach_final_cents int
)
LANGUAGE plpgsql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_line jsonb; v_kind text; v_ref uuid; v_seats int;
    v_prices_id uuid; v_label text; v_cap int;
    v_bd record; v_ach_bd record; v_ach_ok boolean;
BEGIN
    IF p_lines IS NULL OR jsonb_typeof(p_lines) <> 'array' THEN
        RETURN;
    END IF;

    -- ACH offered only when the tenant enabled it AND this event opted in.
    SELECT COALESCE(t.ach_enabled AND e.ach_enabled, false) INTO v_ach_ok
      FROM events e JOIN tenants t ON t.tenants_id = e.tenants_id
     WHERE e.events_id = p_event_id;

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

        SELECT * INTO v_bd FROM app.price_breakdown(v_prices_id, now(), v_seats,
                                 app.remaining_for_price(v_prices_id));

        kind := v_kind; ref_id := v_ref; label := COALESCE(v_label, ''); seats := v_seats;
        base_price_cents := v_bd.base_price_cents;
        selling_price_cents := v_bd.selling_price_cents;
        discount_cents := v_bd.discount_cents;
        applied_price_rules_id := v_bd.applied_price_rules_id;
        applied_rule_name := v_bd.applied_rule_name;
        platform_fee_cents := v_bd.platform_fee_cents;
        gateway_fee_cents := v_bd.gateway_fee_cents;
        tax_cents := v_bd.tax_cents;
        final_price_cents := v_bd.final_price_cents;
        organizer_net_cents := v_bd.organizer_net_cents;
        currency := v_bd.currency;

        ach_available := v_ach_ok;
        IF v_ach_ok THEN
            SELECT * INTO v_ach_bd FROM app.price_breakdown_for_method(v_prices_id, now(), v_seats,
                                         app.remaining_for_price(v_prices_id), 'ach');
            ach_final_cents := v_ach_bd.final_price_cents;
        ELSE
            ach_final_cents := v_bd.final_price_cents;
        END IF;
        RETURN NEXT;
    END LOOP;
END; $$;
