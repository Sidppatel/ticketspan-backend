-- Resolves the service fee svyne charges on a single unit of price, given a
-- developer-defined fee formula. fee = round(price * percent_bps / 10000) +
-- flat_cents, clamped to [min_fee_cents, max_fee_cents] when those are set.
-- Returns 0 when no formula is attached. Single source of truth: used by the
-- ticket-type/table create/update SPs (to resolve platform_fee_cents) and by
-- the booking SPs (to recompute the fee server-side, ignoring client input).
CREATE OR REPLACE FUNCTION app.compute_fee(p_price_cents int, p_formula uuid)
RETURNS int
LANGUAGE plpgsql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_pct int; v_flat int; v_min int; v_max int; v_active bool;
    v_fee int;
BEGIN
    IF p_formula IS NULL OR p_price_cents IS NULL THEN
        RETURN 0;
    END IF;

    SELECT percent_bps, flat_cents, min_fee_cents, max_fee_cents, is_active
      INTO v_pct, v_flat, v_min, v_max, v_active
      FROM fee_formulas
      WHERE fee_formulas_id = p_formula;

    IF NOT FOUND OR v_active = false THEN
        RETURN 0;
    END IF;

    v_fee := round(p_price_cents::numeric * v_pct / 10000)::int + v_flat;
    IF v_min IS NOT NULL AND v_fee < v_min THEN v_fee := v_min; END IF;
    IF v_max IS NOT NULL AND v_fee > v_max THEN v_fee := v_max; END IF;
    IF v_fee < 0 THEN v_fee := 0; END IF;
    RETURN v_fee;
END; $$;
