CREATE OR REPLACE FUNCTION sp_create_price(
    p_event_id uuid, p_name text, p_pricing_type text, p_base_price_cents int,
    p_per_attendee_cents int, p_is_all_inclusive bool, p_fee_formulas_id uuid,
    p_parent_prices_id uuid, p_max_quantity int
) RETURNS uuid LANGUAGE sql
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT app.create_price(p_event_id, p_name, p_pricing_type, p_base_price_cents,
        p_per_attendee_cents, p_is_all_inclusive, p_fee_formulas_id,
        p_parent_prices_id, p_max_quantity);
$$;
