CREATE OR REPLACE FUNCTION sp_create_purchase(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_seats int,
    p_event_ticket_type_id uuid,
    p_subtotal_cents int, p_fee_cents int, p_total_cents int,
    p_purchase_number text, p_status text DEFAULT 'Pending'
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid; v_tenant uuid;
BEGIN
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_event_id;

    INSERT INTO purchases (tenants_id, purchase_number, status, users_id, events_id, tables_id,
        seats_reserved, event_ticket_types_id, subtotal_cents, fee_cents, total_cents,
        created_at, updated_at)
    VALUES (v_tenant, p_purchase_number, p_status, p_user_id, p_event_id, p_table_id,
        p_seats, p_event_ticket_type_id, p_subtotal_cents, p_fee_cents, p_total_cents,
        now(), now())
    RETURNING purchases_id INTO v_id;

    IF p_table_id IS NOT NULL THEN
        INSERT INTO purchase_tables (tenants_id, purchases_id, tables_id)
        VALUES (v_tenant, v_id, p_table_id);
    END IF;

    RETURN v_id;
END; $$;
