CREATE OR REPLACE FUNCTION sp_update_event_ticket_type(
    p_id uuid, p_label text, p_price_cents int,
    p_platform_fee_cents int, p_max_quantity int, p_sort_order int, p_is_active bool,
    p_description text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE event_ticket_types SET
        label = COALESCE(p_label, label),
        price_cents = COALESCE(p_price_cents, price_cents),
        platform_fee_cents = p_platform_fee_cents,
        max_quantity = p_max_quantity,
        sort_order = COALESCE(p_sort_order, sort_order),
        description = COALESCE(p_description, description),
        is_active = COALESCE(p_is_active, is_active),
        updated_at = now()
    WHERE event_ticket_types_id = p_id;
END; $$;