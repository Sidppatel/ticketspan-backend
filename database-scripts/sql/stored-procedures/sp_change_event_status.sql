CREATE OR REPLACE FUNCTION sp_change_event_status(
    p_id uuid, p_status text, p_scheduled_publish_at timestamptz DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_current text; v_sold int; v_tenant uuid;
BEGIN
    SELECT status, tenants_id INTO v_current, v_tenant FROM events WHERE events_id = p_id;

    IF p_status = 'Published' THEN
        PERFORM app.assert_tenant_sellable(v_tenant);
    END IF;

    IF p_status = 'Draft' AND v_current IS DISTINCT FROM 'Draft' THEN
        SELECT COALESCE(SUM(bl.seats), 0)::int INTO v_sold
          FROM booking_lines bl
          JOIN bookings b ON b.bookings_id = bl.bookings_id
         WHERE b.events_id = p_id
           AND b.status IN ('Pending', 'Paid', 'CheckedIn');
        IF v_sold > 0 THEN
            RAISE EXCEPTION 'This event has % tickets sold and cannot be reverted to draft.', v_sold;
        END IF;
    END IF;

    UPDATE events SET
        status = p_status,
        published_at = CASE WHEN p_status = 'Published' AND published_at IS NULL THEN now() ELSE published_at END,
        scheduled_publish_at = p_scheduled_publish_at,
        updated_at = now()
    WHERE events_id = p_id;
END; $$;
