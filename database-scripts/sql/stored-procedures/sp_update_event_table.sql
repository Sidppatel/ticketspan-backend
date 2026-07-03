DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool);
DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool, int);
DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool, int, int, int);
DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool, uuid, int, int);

CREATE OR REPLACE FUNCTION sp_update_event_table(
    p_id uuid, p_label text, p_capacity int, p_shape text, p_color text,
    p_price_cents int, p_is_active bool, p_fee_formulas_id uuid,
    p_width numeric DEFAULT NULL, p_height numeric DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_price int; v_formula uuid;
    v_old_price int; v_old_formula uuid; v_old_capacity int; v_sold int;
BEGIN
    SELECT COALESCE(p_price_cents, price_cents),
           app.resolve_fee_formula(p_fee_formulas_id, events_id, tenants_id),
           price_cents, fee_formulas_id, capacity
      INTO v_price, v_formula, v_old_price, v_old_formula, v_old_capacity
      FROM event_tables WHERE event_tables_id = p_id;

    SELECT COUNT(*)::int INTO v_sold
      FROM tables
     WHERE event_tables_id = p_id
       AND (status = 'Booked' OR (status = 'Locked' AND lock_expires_at > now()));

    IF v_sold > 0 THEN
        IF (p_price_cents IS NOT NULL AND p_price_cents IS DISTINCT FROM v_old_price)
           OR (p_fee_formulas_id IS DISTINCT FROM v_old_formula) THEN
            RAISE EXCEPTION 'This table type has % sold or held tables and its price cannot be modified.', v_sold;
        END IF;
        IF p_capacity IS NOT NULL AND p_capacity < v_old_capacity THEN
            RAISE EXCEPTION 'This table type has % sold or held tables and its capacity cannot be reduced.', v_sold;
        END IF;
    END IF;

    UPDATE event_tables SET
        label = COALESCE(p_label, label),
        capacity = COALESCE(p_capacity, capacity),
        shape = COALESCE(p_shape, shape),
        color = CASE WHEN p_color IS NOT NULL THEN p_color ELSE color END,
        price_cents = COALESCE(p_price_cents, price_cents),
        is_active = COALESCE(p_is_active, is_active),
        fee_formulas_id = p_fee_formulas_id,
        platform_fee_cents = app.compute_fee(v_price, v_formula),
        default_width = COALESCE(p_width, default_width),
        default_height = COALESCE(p_height, default_height),
        updated_at = now()
    WHERE event_tables_id = p_id;
END; $$;
