DROP FUNCTION IF EXISTS sp_uncheck_in_ticket(uuid, uuid, uuid, text);

CREATE OR REPLACE FUNCTION sp_uncheck_in_ticket(
    p_ticket_id uuid,
    p_event_id uuid,
    p_staff_user_id uuid,
    p_reason text
)
RETURNS TABLE(
    success boolean,
    message text,
    guest_name text,
    status_str text
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_booking_id uuid;
    v_ticket_status text;
    v_seat_number int;
    v_guest_user_id uuid;
    v_event_id uuid;
    v_guest_name text;
    v_restored_status text;
BEGIN
    IF p_reason IS NULL OR btrim(p_reason) = '' THEN
        RETURN QUERY SELECT false, 'A reason is required to undo a check-in'::text, NULL::text, NULL::text;
        RETURN;
    END IF;

    SELECT t.bookings_id, t.status::text, t.seat_number, t.guest_users_id, t.events_id
      INTO v_booking_id, v_ticket_status, v_seat_number, v_guest_user_id, v_event_id
    FROM booking_lines t
    WHERE t.booking_lines_id = p_ticket_id AND t.kind = 'Ticket'
    FOR UPDATE;

    IF NOT FOUND THEN
        PERFORM sp_log_checkin_attempt(p_event_id, p_staff_user_id, NULL, NULL, 'uncheck', 'failed', 'invalid_ticket');
        RETURN QUERY SELECT false, 'Ticket not found'::text, NULL::text, NULL::text;
        RETURN;
    END IF;

    IF v_event_id <> p_event_id THEN
        PERFORM sp_log_checkin_attempt(p_event_id, p_staff_user_id, v_booking_id, p_ticket_id, 'uncheck', 'failed', 'wrong_event');
        RETURN QUERY SELECT false, 'Ticket is for a different event'::text, NULL::text, NULL::text;
        RETURN;
    END IF;

    IF v_ticket_status <> 'CheckedIn' THEN
        PERFORM sp_log_checkin_attempt(p_event_id, p_staff_user_id, v_booking_id, p_ticket_id, 'uncheck', 'failed', 'not_checked_in');
        RETURN QUERY SELECT false, 'Ticket is not checked in'::text, NULL::text, v_ticket_status;
        RETURN;
    END IF;

    IF v_guest_user_id IS NOT NULL THEN
        v_restored_status := 'Claimed';
        SELECT u.first_name || ' ' || u.last_name INTO v_guest_name
        FROM users u WHERE u.users_id = v_guest_user_id;
    ELSE
        v_restored_status := 'Unassigned';
        SELECT u.first_name || ' ' || u.last_name INTO v_guest_name
        FROM users u
        JOIN bookings b ON b.users_id = u.users_id
        WHERE b.bookings_id = v_booking_id;
    END IF;

    UPDATE booking_lines
       SET status = v_restored_status, updated_at = now()
     WHERE booking_lines_id = p_ticket_id;

    UPDATE bookings
       SET status = 'Paid', updated_at = now()
     WHERE bookings_id = v_booking_id AND status = 'CheckedIn';

    PERFORM sp_log_checkin_attempt(p_event_id, p_staff_user_id, v_booking_id, p_ticket_id, 'uncheck', 'success', btrim(p_reason));

    RETURN QUERY SELECT
        true,
        ('Check-in undone — Seat #' || v_seat_number || ' is now ' || v_restored_status)::text,
        v_guest_name,
        v_restored_status;
END;
$$;
