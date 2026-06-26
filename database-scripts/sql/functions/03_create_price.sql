-- Reusable Pricing Module insert. Single place that builds a `prices` row so the
-- public sp_create_price and the sellable-creation SPs (event tables, ticket
-- types) all link to a real Price the same way. Returns the new prices_id.
CREATE OR REPLACE FUNCTION app.create_price(
    p_event_id uuid, p_name text, p_pricing_type text, p_base_price_cents int,
    p_per_attendee_cents int, p_is_all_inclusive bool, p_fee_formulas_id uuid,
    p_parent_prices_id uuid, p_max_quantity int
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO prices (tenants_id, events_id, name, pricing_type, base_price_cents,
        per_attendee_cents, is_all_inclusive, fee_formulas_id, parent_prices_id,
        max_quantity, is_active, created_at, updated_at)
    VALUES ((SELECT tenants_id FROM events WHERE events_id = p_event_id),
        p_event_id, p_name, p_pricing_type, p_base_price_cents,
        COALESCE(p_per_attendee_cents, 0), COALESCE(p_is_all_inclusive, false),
        p_fee_formulas_id, p_parent_prices_id, p_max_quantity, true, now(), now())
    RETURNING prices_id INTO v_id;
    RETURN v_id;
END; $$;
