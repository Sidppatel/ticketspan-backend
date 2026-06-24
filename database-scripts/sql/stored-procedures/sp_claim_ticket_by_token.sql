CREATE OR REPLACE FUNCTION sp_claim_ticket_by_token(
    p_invite_hash text, p_guest_user_id uuid
)
RETURNS TABLE(ticket_id uuid, success boolean, message text, already_by_me boolean)
LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
    v_booking_id uuid;
    v_status text;
    v_guest_user_id uuid;
    v_expires_at timestamptz;
    v_already_count int;
BEGIN
    SELECT id, bookings_id, status::text, guest_users_id, invite_expires_at
        INTO v_id, v_booking_id, v_status, v_guest_user_id, v_expires_at
        FROM tickets
        WHERE invite_token_hash = p_invite_hash
        FOR UPDATE;

    IF v_id IS NULL THEN
        RETURN QUERY SELECT NULL::uuid, false, 'Invalid or expired invite link', false;
        RETURN;
    END IF;

    IF v_expires_at IS NOT NULL AND v_expires_at < now() THEN
        RETURN QUERY SELECT v_id, false, 'This invite link has expired', false;
        RETURN;
    END IF;

    IF v_status = 'CheckedIn' THEN
        RETURN QUERY SELECT v_id, false, 'This ticket has already been used', false;
        RETURN;
    END IF;

    IF v_guest_user_id = p_guest_user_id THEN
        RETURN QUERY SELECT v_id, true, 'You have already claimed this ticket', true;
        RETURN;
    END IF;

    IF v_status = 'Claimed' THEN
        RETURN QUERY SELECT v_id, false, 'This ticket has already been claimed', false;
        RETURN;
    END IF;

    SELECT COUNT(*) INTO v_already_count
        FROM tickets
        WHERE bookings_id = v_booking_id
          AND guest_users_id = p_guest_user_id
          AND tickets_id <> v_id
          AND status IN ('Claimed', 'CheckedIn');

    IF v_already_count > 0 THEN
        RETURN QUERY SELECT v_id, false, 'You already have a ticket on this booking. One ticket per person.', false;
        RETURN;
    END IF;

    UPDATE tickets SET
        guest_users_id = p_guest_user_id,
        claimed_at = now(),
        status = 'Claimed',
        invite_token_hash = NULL,
        updated_at = now()
    WHERE tickets_id = v_id;

    RETURN QUERY SELECT v_id, true, 'Ticket claimed successfully', false;
END; $$;
