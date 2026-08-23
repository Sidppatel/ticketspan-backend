DROP FUNCTION IF EXISTS sp_lookup_booking_for_checkin(text, uuid);

CREATE OR REPLACE FUNCTION sp_lookup_booking_for_checkin(
    p_code text,
    p_event_id uuid
)
RETURNS TABLE(
    bookings_id uuid
) LANGUAGE plpgsql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_clean text := trim(p_code);
    v_user_id uuid;
    v_booking_id uuid;
BEGIN
    -- 1. Direct booking number or booking qr_token match
    SELECT b.bookings_id INTO v_booking_id
    FROM bookings b
    WHERE b.events_id = p_event_id
      AND (b.booking_number = v_clean OR b.qr_token = v_clean)
    LIMIT 1;

    IF v_booking_id IS NOT NULL THEN
        RETURN QUERY SELECT v_booking_id;
        RETURN;
    END IF;

    -- 2. Direct ticket code or ticket qr_token match
    SELECT bl.bookings_id INTO v_booking_id
    FROM booking_lines bl
    WHERE bl.events_id = p_event_id
      AND bl.kind = 'Ticket'
      AND (bl.ticket_code = v_clean OR bl.qr_token = v_clean)
    LIMIT 1;

    IF v_booking_id IS NOT NULL THEN
        RETURN QUERY SELECT v_booking_id;
        RETURN;
    END IF;

    -- 3. JSON Universal Pass QR
    IF v_clean LIKE '{%' OR v_clean LIKE '%universal_attendee_credential%' THEN
        BEGIN
            v_user_id := (v_clean::jsonb->>'uid')::uuid;
        EXCEPTION WHEN OTHERS THEN
            v_user_id := NULL;
        END;

        IF v_user_id IS NOT NULL THEN
            SELECT b.bookings_id INTO v_booking_id
            FROM bookings b
            WHERE b.events_id = p_event_id
              AND (b.users_id = v_user_id OR EXISTS (
                  SELECT 1 FROM booking_lines bl 
                  WHERE bl.bookings_id = b.bookings_id AND bl.guest_users_id = v_user_id
              ))
            LIMIT 1;

            IF v_booking_id IS NOT NULL THEN
                RETURN QUERY SELECT v_booking_id;
                RETURN;
            END IF;
        END IF;
    END IF;

    -- 4. Pass ID TS-XXXX-XXX or Email or User UUID
    IF v_clean LIKE 'TS-%' OR v_clean LIKE '%@%' OR v_clean ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN
        IF v_clean ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN
            v_user_id := v_clean::uuid;
        ELSIF v_clean LIKE '%@%' THEN
            SELECT u.users_id INTO v_user_id FROM users u WHERE lower(u.email) = lower(v_clean) LIMIT 1;
        ELSIF v_clean LIKE 'TS-%' THEN
            SELECT u.users_id INTO v_user_id 
            FROM users u 
            WHERE upper(substring(u.users_id::text, 1, 8)) = upper(split_part(v_clean, '-', 2))
            LIMIT 1;
        END IF;

        IF v_user_id IS NOT NULL THEN
            SELECT b.bookings_id INTO v_booking_id
            FROM bookings b
            WHERE b.events_id = p_event_id
              AND (b.users_id = v_user_id OR EXISTS (
                  SELECT 1 FROM booking_lines bl 
                  WHERE bl.bookings_id = b.bookings_id AND bl.guest_users_id = v_user_id
              ))
            LIMIT 1;

            IF v_booking_id IS NOT NULL THEN
                RETURN QUERY SELECT v_booking_id;
                RETURN;
            END IF;
        END IF;
    END IF;

    RETURN;
END;
$$;
