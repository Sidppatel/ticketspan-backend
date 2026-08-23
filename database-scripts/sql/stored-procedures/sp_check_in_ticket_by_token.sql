DROP FUNCTION IF EXISTS sp_check_in_ticket_by_token(text, uuid, uuid);

CREATE OR REPLACE FUNCTION sp_check_in_ticket_by_token(
    p_qr_token text,
    p_event_id uuid,
    p_staff_user_id uuid,
    p_method text DEFAULT 'qr_scan'
)
RETURNS TABLE(
    success boolean,
    message text,
    booking_number text,
    guest_name text,
    event_title text,
    status_str text,
    checked_in_at timestamptz
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_ticket_id uuid;
    v_user_id uuid;
    v_clean_token text := trim(p_qr_token);
BEGIN
    -- 1. Direct qr_token match on booking_lines
    SELECT booking_lines_id INTO v_ticket_id
    FROM booking_lines
    WHERE qr_token = v_clean_token AND kind = 'Ticket';

    -- 2. Check if token is a JSON Universal Pass credential
    IF v_ticket_id IS NULL AND (v_clean_token LIKE '{%' OR v_clean_token LIKE '%universal_attendee_credential%') THEN
        BEGIN
            v_user_id := (v_clean_token::jsonb->>'uid')::uuid;
        EXCEPTION WHEN OTHERS THEN
            v_user_id := NULL;
        END;

        IF v_user_id IS NOT NULL THEN
            -- First search for an active unchecked-in ticket for this event
            SELECT bl.booking_lines_id INTO v_ticket_id
            FROM booking_lines bl
            JOIN bookings b ON b.bookings_id = bl.bookings_id
            WHERE bl.events_id = p_event_id
              AND bl.kind = 'Ticket'
              AND (bl.guest_users_id = v_user_id OR (bl.guest_users_id IS NULL AND b.users_id = v_user_id))
              AND bl.status <> 'CheckedIn'
            ORDER BY bl.seat_number ASC
            LIMIT 1;

            -- If all are already checked in or not in unchecked state, select any for feedback
            IF v_ticket_id IS NULL THEN
                SELECT bl.booking_lines_id INTO v_ticket_id
                FROM booking_lines bl
                JOIN bookings b ON b.bookings_id = bl.bookings_id
                WHERE bl.events_id = p_event_id
                  AND bl.kind = 'Ticket'
                  AND (bl.guest_users_id = v_user_id OR (bl.guest_users_id IS NULL AND b.users_id = v_user_id))
                ORDER BY bl.seat_number ASC
                LIMIT 1;
            END IF;
        END IF;
    END IF;

    -- 3. Check if token is a UUID matching user ID
    IF v_ticket_id IS NULL AND v_clean_token ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN
        v_user_id := v_clean_token::uuid;
        SELECT bl.booking_lines_id INTO v_ticket_id
        FROM booking_lines bl
        JOIN bookings b ON b.bookings_id = bl.bookings_id
        WHERE bl.events_id = p_event_id
          AND bl.kind = 'Ticket'
          AND (bl.guest_users_id = v_user_id OR (bl.guest_users_id IS NULL AND b.users_id = v_user_id))
        ORDER BY (bl.status = 'CheckedIn'), bl.seat_number ASC
        LIMIT 1;
    END IF;

    -- 4. Check if token is a Pass ID TS-XXXX-XXX or email
    IF v_ticket_id IS NULL AND (v_clean_token LIKE 'TS-%' OR v_clean_token LIKE '%@%') THEN
        IF v_clean_token LIKE '%@%' THEN
            SELECT u.users_id INTO v_user_id FROM users u WHERE lower(u.email) = lower(v_clean_token) LIMIT 1;
        ELSE
            -- Pass ID format: TS-<first8OfUsersId>-<emailPrefix>
            SELECT u.users_id INTO v_user_id 
            FROM users u 
            WHERE upper(substring(u.users_id::text, 1, 8)) = upper(split_part(v_clean_token, '-', 2))
            LIMIT 1;
        END IF;

        IF v_user_id IS NOT NULL THEN
            SELECT bl.booking_lines_id INTO v_ticket_id
            FROM booking_lines bl
            JOIN bookings b ON b.bookings_id = bl.bookings_id
            WHERE bl.events_id = p_event_id
              AND bl.kind = 'Ticket'
              AND (bl.guest_users_id = v_user_id OR (bl.guest_users_id IS NULL AND b.users_id = v_user_id))
            ORDER BY (bl.status = 'CheckedIn'), bl.seat_number ASC
            LIMIT 1;
        END IF;
    END IF;

    IF v_ticket_id IS NULL THEN
        PERFORM sp_log_checkin_attempt(p_event_id, p_staff_user_id, NULL, NULL, p_method, 'failed', 'invalid_ticket');
        RETURN QUERY SELECT false, 'Ticket not found'::text, NULL::text, NULL::text, NULL::text, NULL::text, NULL::timestamptz;
        RETURN;
    END IF;

    RETURN QUERY SELECT * FROM sp_check_in_ticket(v_ticket_id, p_event_id, p_staff_user_id, p_method);
END;
$$;
