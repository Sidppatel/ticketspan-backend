CREATE OR REPLACE FUNCTION sp_create_event_ticket_type(
    p_event_id uuid, p_label text, p_price_cents int,
    p_platform_fee_cents int, p_max_quantity int, p_sort_order int,
    p_description text DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO event_ticket_types (tenants_id, events_id, label, price_cents, platform_fee_cents,
        max_quantity, sort_order, description, is_active, created_at, updated_at)
    VALUES ((SELECT tenants_id FROM events WHERE events_id = p_event_id),
        p_event_id, p_label, p_price_cents, p_platform_fee_cents,
        p_max_quantity, p_sort_order, p_description, true, now(), now())
    RETURNING event_ticket_types_id INTO v_id;
    RETURN v_id;
END; $$;
