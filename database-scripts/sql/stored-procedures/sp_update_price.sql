-- Updates a price. When p_allow_fee_override is false the fee formula override is
-- left untouched (admins cannot change the developer fee assignment). Refreshes the
-- cached price snapshot on any linked ticket type / table.
CREATE OR REPLACE FUNCTION sp_update_price(
    p_prices_id uuid, p_name text, p_base_price_cents int, p_per_attendee_cents int,
    p_is_all_inclusive bool, p_max_quantity int, p_is_active bool,
    p_fee_formulas_id uuid, p_allow_fee_override bool
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_tenant uuid; v_formula uuid; v_fee int;
BEGIN
    UPDATE prices SET
        name = p_name,
        base_price_cents = p_base_price_cents,
        per_attendee_cents = COALESCE(p_per_attendee_cents, 0),
        is_all_inclusive = COALESCE(p_is_all_inclusive, false),
        max_quantity = p_max_quantity,
        is_active = COALESCE(p_is_active, true),
        fee_formulas_id = CASE WHEN p_allow_fee_override THEN p_fee_formulas_id ELSE fee_formulas_id END,
        updated_at = now()
    WHERE prices_id = p_prices_id
    RETURNING tenants_id, fee_formulas_id INTO v_tenant, v_formula;

    v_formula := app.resolve_fee_formula(v_formula, v_tenant);
    v_fee := app.compute_fee(p_base_price_cents, v_formula);

    UPDATE event_ticket_types SET price_cents = p_base_price_cents,
        platform_fee_cents = v_fee, updated_at = now()
    WHERE prices_id = p_prices_id;
    UPDATE event_tables SET price_cents = p_base_price_cents,
        platform_fee_cents = v_fee, updated_at = now()
    WHERE prices_id = p_prices_id;
END; $$;
