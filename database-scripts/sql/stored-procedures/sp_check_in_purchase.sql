CREATE OR REPLACE FUNCTION sp_check_in_purchase(p_qr_token text)
RETURNS TABLE(
    success boolean,
    message text,
    purchase_number text,
    guest_name text,
    event_title text,
    status_str text,
    checked_in_at timestamptz
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_purchase_id uuid;
    v_purchase_number text;
    v_purchase_status text;
    v_updated_at timestamptz;
    v_event_title text;
    v_user_name text;
BEGIN
    SELECT p.purchases_id, p.purchase_number, p.status, p.updated_at,
           e.title, u.first_name || ' ' || u.last_name
      INTO v_purchase_id, v_purchase_number, v_purchase_status, v_updated_at,
           v_event_title, v_user_name
    FROM purchases p
    JOIN events e ON e.events_id = p.events_id
    JOIN users u ON u.users_id = p.users_id
    WHERE p.qr_token = p_qr_token
    FOR UPDATE OF p;

    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF v_purchase_status = 'CheckedIn' THEN
        RETURN QUERY SELECT
            false, 'Already checked in'::text,
            v_purchase_number, v_user_name, v_event_title,
            'CheckedIn'::text, v_updated_at;
        RETURN;
    END IF;

    IF v_purchase_status <> 'Paid' THEN
        RETURN QUERY SELECT
            false,
            ('Purchase is ' || v_purchase_status || ' — cannot check in')::text,
            v_purchase_number, v_user_name, v_event_title,
            v_purchase_status::text, NULL::timestamptz;
        RETURN;
    END IF;

    UPDATE purchases
       SET status = 'CheckedIn', updated_at = now()
     WHERE purchases_id = v_purchase_id;

    RETURN QUERY SELECT
        true, 'Check-in successful'::text,
        v_purchase_number, v_user_name, v_event_title,
        'CheckedIn'::text, now();
END;
$$;
