-- Developer-managed fee formula CRUD + assignment. Writes are gated to
-- developers by the fee_formulas RLS policy (WITH CHECK app.is_developer()).

CREATE OR REPLACE FUNCTION sp_create_fee_formula(
    p_name text, p_percent_bps int, p_flat_cents int,
    p_min_fee_cents int DEFAULT NULL, p_max_fee_cents int DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO fee_formulas (fee_formulas_id, name, percent_bps, flat_cents,
        min_fee_cents, max_fee_cents, is_active, created_at, updated_at)
    VALUES (gen_random_uuid(), p_name, COALESCE(p_percent_bps, 0), COALESCE(p_flat_cents, 0),
        p_min_fee_cents, p_max_fee_cents, true, now(), now())
    RETURNING fee_formulas_id INTO v_id;
    RETURN v_id;
END; $$;

CREATE OR REPLACE FUNCTION sp_update_fee_formula(
    p_id uuid, p_name text, p_percent_bps int, p_flat_cents int,
    p_min_fee_cents int, p_max_fee_cents int, p_is_active bool
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE fee_formulas SET
        name = COALESCE(p_name, name),
        percent_bps = COALESCE(p_percent_bps, percent_bps),
        flat_cents = COALESCE(p_flat_cents, flat_cents),
        min_fee_cents = p_min_fee_cents,
        max_fee_cents = p_max_fee_cents,
        is_active = COALESCE(p_is_active, is_active),
        updated_at = now()
    WHERE fee_formulas_id = p_id;

    -- Re-resolve cached platform_fee_cents on everything using this formula.
    UPDATE event_ticket_types
       SET platform_fee_cents = app.compute_fee(price_cents, p_id), updated_at = now()
     WHERE fee_formulas_id = p_id;
    UPDATE event_tables
       SET platform_fee_cents = app.compute_fee(price_cents, p_id), updated_at = now()
     WHERE fee_formulas_id = p_id;
END; $$;

CREATE OR REPLACE FUNCTION sp_delete_fee_formula(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    -- Detaching first (FK is ON DELETE SET NULL, but clear the cached fee too).
    UPDATE event_ticket_types
       SET fee_formulas_id = NULL, platform_fee_cents = 0, updated_at = now()
     WHERE fee_formulas_id = p_id;
    UPDATE event_tables
       SET fee_formulas_id = NULL, platform_fee_cents = 0, updated_at = now()
     WHERE fee_formulas_id = p_id;
    DELETE FROM fee_formulas WHERE fee_formulas_id = p_id;
END; $$;

-- Attach a formula to a ticket type ('ticket') or table ('table') and resolve
-- its cached platform_fee_cents. p_formula NULL clears the formula (fee 0).
CREATE OR REPLACE FUNCTION sp_set_fee_formula(
    p_kind text, p_target uuid, p_formula uuid
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    IF p_kind = 'ticket' THEN
        UPDATE event_ticket_types
           SET fee_formulas_id = p_formula,
               platform_fee_cents = app.compute_fee(price_cents, p_formula),
               updated_at = now()
         WHERE event_ticket_types_id = p_target;
    ELSIF p_kind = 'table' THEN
        UPDATE event_tables
           SET fee_formulas_id = p_formula,
               platform_fee_cents = app.compute_fee(price_cents, p_formula),
               updated_at = now()
         WHERE event_tables_id = p_target;
    ELSE
        RAISE EXCEPTION 'Unknown target kind %', p_kind USING ERRCODE = '22023';
    END IF;
END; $$;
