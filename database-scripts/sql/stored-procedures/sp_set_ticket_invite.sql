CREATE OR REPLACE FUNCTION sp_set_ticket_invite(
    p_ticket_id uuid, p_invite_hash text, p_email text, p_expires_at timestamptz
) RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE 
    v_updated int;
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
        RETURN false;
    END IF;

    UPDATE booking_lines SET
        invite_token_hash = p_invite_hash,
        invited_email = p_email,
        invite_sent_at = now(),
        invite_expires_at = p_expires_at,
        status = 'Invited',
        guest_users_id = NULL,
        claimed_at = NULL,
        updated_at = now()
    WHERE booking_lines_id = p_ticket_id AND kind = 'Ticket'
      AND status IN ('Unassigned', 'Invited');
    GET DIAGNOSTICS v_updated = ROW_COUNT;
    RETURN v_updated > 0;
END; $$;

