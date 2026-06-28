DROP FUNCTION IF EXISTS sp_create_schedule_item(uuid, uuid, text, text, timestamptz, timestamptz);

CREATE OR REPLACE FUNCTION sp_create_schedule_item(
    p_events_id uuid, p_tenants_id uuid, p_title text, p_type_category text,
    p_start_time timestamptz, p_end_time timestamptz
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
    v_event_start timestamptz;
    v_event_end timestamptz;
BEGIN
    IF p_end_time <= p_start_time THEN
        RAISE EXCEPTION 'Schedule item end time must be after its start time' USING ERRCODE = '22023';
    END IF;

    SELECT start_date, end_date INTO v_event_start, v_event_end
    FROM events WHERE events_id = p_events_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Event not found' USING ERRCODE = 'P0002';
    END IF;

    IF p_start_time < v_event_start OR p_end_time > v_event_end THEN
        RAISE EXCEPTION 'Schedule item must fall within the event time window' USING ERRCODE = '22023';
    END IF;

    IF EXISTS (
        SELECT 1 FROM schedule_items
        WHERE events_id = p_events_id
          AND p_start_time < end_time
          AND start_time < p_end_time
    ) THEN
        RAISE EXCEPTION 'Schedule item overlaps an existing item' USING ERRCODE = '23P01';
    END IF;

    INSERT INTO schedule_items (events_id, tenants_id, title, type_category,
        start_time, end_time, created_at, updated_at)
    VALUES (p_events_id, p_tenants_id, p_title, p_type_category,
        p_start_time, p_end_time, now(), now())
    RETURNING schedule_items_id INTO v_id;

    RETURN v_id;
END; $$;
