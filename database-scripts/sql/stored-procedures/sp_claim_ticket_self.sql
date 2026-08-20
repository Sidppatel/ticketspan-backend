CREATE OR REPLACE FUNCTION sp_claim_ticket_self(
    p_ticket_id uuid, p_user_id uuid
) RETURNS TABLE(success boolean, message text) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_booking_id uuid;
    v_booking_user_id uuid;
    v_status text;
    v_already_count int;
    v_updated int;
BEGIN
    SELECT bl.bookings_id, b.users_id, bl.status::text
        INTO v_booking_id, v_booking_user_id, v_status
        FROM booking_lines bl
        JOIN bookings b ON bl.bookings_id = b.bookings_id
        WHERE bl.booking_lines_id = p_ticket_id AND bl.kind = 'Ticket'
        FOR UPDATE;

    IF v_booking_id IS NULL THEN
        RETURN QUERY SELECT false, 'Ticket not found';
        RETURN;
    END IF;

    IF v_booking_user_id <> p_user_id AND NOT app.is_developer() THEN
        RETURN QUERY SELECT false, 'Only the booking purchaser can claim this ticket for themselves.';
        RETURN;
    END IF;

    IF v_status NOT IN ('Unassigned', 'Invited') THEN
        RETURN QUERY SELECT false, 'This ticket has already been claimed. Revoke it first.';
        RETURN;
    END IF;


    SELECT COUNT(*) INTO v_already_count
        FROM booking_lines
        WHERE bookings_id = v_booking_id AND kind = 'Ticket'
          AND guest_users_id = p_user_id
          AND booking_lines_id <> p_ticket_id
          AND status IN ('Claimed', 'CheckedIn');

    IF v_already_count > 0 THEN
        RETURN QUERY SELECT false, 'You already have a ticket on this booking. One ticket per person.';
        RETURN;
    END IF;

    UPDATE booking_lines SET
        guest_users_id = p_user_id,
        status = 'Claimed',
        claimed_at = now(),
        invite_token_hash = NULL,
        invite_expires_at = NULL,
        invited_email = NULL,
        invite_sent_at = NULL,
        updated_at = now()
    WHERE booking_lines_id = p_ticket_id
      AND status IN ('Unassigned', 'Invited');
    GET DIAGNOSTICS v_updated = ROW_COUNT;

    IF v_updated = 0 THEN
        RETURN QUERY SELECT false, 'This ticket has already been claimed. Revoke it first.';
        RETURN;
    END IF;

    RETURN QUERY SELECT true, 'Ticket claimed';
END; $$;
