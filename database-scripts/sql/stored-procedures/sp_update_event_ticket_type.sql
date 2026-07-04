DROP FUNCTION IF EXISTS sp_update_event_ticket_type(uuid, text, int, int, int, int, bool, text);
DROP FUNCTION IF EXISTS sp_update_event_ticket_type(uuid, text, int, uuid, int, int, bool, text);
DROP FUNCTION IF EXISTS sp_update_event_ticket_type(uuid, text, int, uuid, int, int, int, bool, text);

CREATE OR REPLACE FUNCTION sp_update_event_ticket_type(
    p_id uuid, p_label text, p_price_cents int,
    p_fee_formulas_id uuid, p_max_quantity int, p_sort_order int, p_capacity int, p_is_active bool,
    p_description text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_price int; v_formula uuid; v_label text; v_prices_id uuid;
    v_old_label text; v_old_price int; v_old_formula uuid; v_sold int; v_old_capacity int;
BEGIN
    SELECT COALESCE(p_price_cents, price_cents),
           app.resolve_fee_formula(p_fee_formulas_id, events_id, tenants_id),
           COALESCE(p_label, label), prices_id,
           label, price_cents, fee_formulas_id, capacity
      INTO v_price, v_formula, v_label, v_prices_id,
           v_old_label, v_old_price, v_old_formula, v_old_capacity
      FROM event_ticket_types WHERE event_ticket_types_id = p_id;

    SELECT COALESCE(SUM(bl.seats), 0)::int INTO v_sold
      FROM booking_lines bl
      JOIN bookings b ON b.bookings_id = bl.bookings_id
     WHERE bl.kind = 'Ticket'
       AND bl.event_ticket_types_id = p_id
       AND b.status IN ('Pending', 'Paid', 'CheckedIn');

    IF v_sold > 0 THEN
        IF (p_price_cents IS NOT NULL AND p_price_cents IS DISTINCT FROM v_old_price)
           OR (p_label IS NOT NULL AND p_label IS DISTINCT FROM v_old_label)
           OR (p_fee_formulas_id IS DISTINCT FROM v_old_formula) THEN
            RAISE EXCEPTION 'This ticket type has % sold tickets and cannot be modified. Please create a new ticket type instead.', v_sold;
        END IF;
        IF p_capacity IS NULL AND v_old_capacity IS NOT NULL THEN
            RAISE EXCEPTION 'Capacity cannot be removed because % tickets are already sold.', v_sold;
        END IF;
        IF p_capacity IS NOT NULL AND p_capacity < v_sold THEN
            RAISE EXCEPTION 'Capacity cannot be reduced below the % tickets already sold for this ticket type.', v_sold;
        END IF;
        IF p_max_quantity IS NOT NULL AND p_max_quantity < v_sold THEN
            RAISE EXCEPTION 'Quantity limit cannot be reduced below the % tickets already sold for this ticket type.', v_sold;
        END IF;
    END IF;

    UPDATE event_ticket_types SET
        label = COALESCE(p_label, label),
        price_cents = COALESCE(p_price_cents, price_cents),
        fee_formulas_id = p_fee_formulas_id,
        platform_fee_cents = app.compute_fee(v_price, v_formula),
        max_quantity = p_max_quantity,
        capacity = p_capacity,
        sort_order = COALESCE(p_sort_order, sort_order),
        description = COALESCE(p_description, description),
        is_active = COALESCE(p_is_active, is_active),
        updated_at = now()
    WHERE event_ticket_types_id = p_id;

    IF v_prices_id IS NOT NULL THEN
        UPDATE prices SET name = v_label, base_price_cents = v_price, updated_at = now()
        WHERE prices_id = v_prices_id;
    END IF;
END; $$;
