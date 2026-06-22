CREATE OR REPLACE FUNCTION sp_confirm_purchase(p_purchase_id uuid, p_qr_token text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_seats int; v_seat int; v_tenant uuid;
BEGIN
    UPDATE purchases SET status = 'Paid', qr_token = p_qr_token, updated_at = now()
    WHERE purchases_id = p_purchase_id AND status = 'Pending'
    RETURNING seats_reserved, tenants_id INTO v_seats, v_tenant;

    IF NOT FOUND THEN
        RETURN;
    END IF;

    UPDATE tables SET status = 'Booked', locked_by_users_id = NULL,
        lock_expires_at = NULL, updated_at = now()
    WHERE tables_id IN (SELECT tables_id FROM purchase_tables WHERE purchases_id = p_purchase_id)
      AND status IN ('Locked', 'Available');

    v_seats := COALESCE(v_seats, 1);
    FOR v_seat IN 1..v_seats LOOP
        INSERT INTO purchase_tickets (tenants_id, purchases_id, ticket_code, qr_token,
            seat_number, status, created_at, updated_at)
        VALUES (v_tenant, p_purchase_id,
            'TKT-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8)),
            encode(gen_random_bytes(32), 'hex'),
            v_seat, 'Unassigned', now(), now());
    END LOOP;
END; $$;
