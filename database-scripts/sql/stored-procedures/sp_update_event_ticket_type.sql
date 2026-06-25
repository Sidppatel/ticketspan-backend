DROP FUNCTION IF EXISTS sp_update_event_ticket_type(uuid, text, int, int, int, int, bool, text);

CREATE OR REPLACE FUNCTION sp_update_event_ticket_type(
    p_id uuid, p_label text, p_price_cents int,
    p_fee_formulas_id uuid, p_max_quantity int, p_sort_order int, p_is_active bool,
    p_description text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_price int;
BEGIN
    v_price := COALESCE(p_price_cents, (SELECT price_cents FROM event_ticket_types WHERE event_ticket_types_id = p_id));
    UPDATE event_ticket_types SET
        label = COALESCE(p_label, label),
        price_cents = COALESCE(p_price_cents, price_cents),
        fee_formulas_id = p_fee_formulas_id,
        platform_fee_cents = app.compute_fee(v_price, p_fee_formulas_id),
        max_quantity = p_max_quantity,
        sort_order = COALESCE(p_sort_order, sort_order),
        description = COALESCE(p_description, description),
        is_active = COALESCE(p_is_active, is_active),
        updated_at = now()
    WHERE event_ticket_types_id = p_id;
END; $$;
