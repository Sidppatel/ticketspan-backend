-- Preview wrapper around app.resolve_price for admin previews and the public
-- floor-plan. p_at defaults to now() when null.
CREATE OR REPLACE FUNCTION sp_calculate_price(
    p_prices_id uuid, p_seats int, p_at timestamptz, p_remaining int
)
RETURNS TABLE(subtotal_cents int, fee_cents int, total_cents int)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT subtotal_cents, fee_cents, total_cents
    FROM app.resolve_price(p_prices_id, COALESCE(p_at, now()), p_seats,
                           COALESCE(p_remaining, app.remaining_for_price(p_prices_id)));
$$;
