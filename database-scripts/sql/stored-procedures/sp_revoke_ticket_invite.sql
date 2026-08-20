CREATE OR REPLACE FUNCTION sp_revoke_ticket_invite(p_ticket_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_booking_user_id uuid;
    v_event_id uuid;
    v_tenant_id uuid;
BEGIN
    SELECT b.users_id, b.events_id, b.tenants_id
        INTO v_booking_user_id, v_event_id, v_tenant_id
        FROM booking_lines bl
        JOIN bookings b ON bl.bookings_id = b.bookings_id
        WHERE bl.booking_lines_id = p_ticket_id AND bl.kind = 'Ticket';

    IF v_booking_user_id IS NULL OR NOT app.can_access_booking(v_booking_user_id, v_event_id, v_tenant_id) THEN
        RETURN;
    END IF;

    UPDATE booking_lines SET
        status = 'Unassigned',
        ticket_code = 'TK-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8)),
        qr_token = encode(gen_random_bytes(32), 'hex'),
        invite_token_hash = NULL,
        invite_expires_at = NULL,
        invited_email = NULL,
        invite_sent_at = NULL,
        guest_users_id = NULL,
        claimed_at = NULL,
        updated_at = now()
    WHERE booking_lines_id = p_ticket_id AND kind = 'Ticket';
END; $$;

