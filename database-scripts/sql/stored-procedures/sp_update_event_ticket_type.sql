DROP FUNCTION IF EXISTS sp_update_event_ticket_type(uuid, text, int, int, int, int, bool, text);

CREATE OR REPLACE FUNCTION sp_update_event_ticket_type(
    p_id uuid, p_label text, p_price_cents int,
    p_fee_formulas_id uuid, p_max_quantity int, p_sort_order int, p_is_active bool,
    p_description text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_price int; v_formula uuid; v_label text; v_prices_id uuid;
BEGIN
    SELECT COALESCE(p_price_cents, price_cents),
           app.resolve_fee_formula(p_fee_formulas_id, tenants_id),
           COALESCE(p_label, label), prices_id
      INTO v_price, v_formula, v_label, v_prices_id
      FROM event_ticket_types WHERE event_ticket_types_id = p_id;
    UPDATE event_ticket_types SET
        label = COALESCE(p_label, label),
        price_cents = COALESCE(p_price_cents, price_cents),
        fee_formulas_id = p_fee_formulas_id,
        platform_fee_cents = app.compute_fee(v_price, v_formula),
        max_quantity = p_max_quantity,
        sort_order = COALESCE(p_sort_order, sort_order),
        description = COALESCE(p_description, description),
        is_active = COALESCE(p_is_active, is_active),
        updated_at = now()
    WHERE event_ticket_types_id = p_id;

    -- Keep the linked Pricing Module price in sync so checkout resolves the new amount.
    IF v_prices_id IS NOT NULL THEN
        UPDATE prices SET name = v_label, base_price_cents = v_price, updated_at = now()
        WHERE prices_id = v_prices_id;
    END IF;
END; $$;
