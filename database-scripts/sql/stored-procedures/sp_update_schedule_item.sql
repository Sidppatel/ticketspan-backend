DROP FUNCTION IF EXISTS sp_update_schedule_item(uuid, text, text, timestamptz, timestamptz);

CREATE OR REPLACE FUNCTION sp_update_schedule_item(
    p_id uuid, p_title text, p_type_category text,
    p_start_time timestamptz, p_end_time timestamptz
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_events_id uuid;
    v_title text;
    v_type_category text;
    v_start_time timestamptz;
    v_end_time timestamptz;
    v_event_start timestamptz;
    v_event_end timestamptz;
BEGIN
    SELECT events_id, title, type_category, start_time, end_time
    INTO v_events_id, v_title, v_type_category, v_start_time, v_end_time
    FROM schedule_items WHERE schedule_items_id = p_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Schedule item not found' USING ERRCODE = 'P0002';
    END IF;

    v_title := COALESCE(NULLIF(trim(p_title), ''), v_title);
    v_type_category := COALESCE(NULLIF(trim(p_type_category), ''), v_type_category);
    v_start_time := COALESCE(p_start_time, v_start_time);
    v_end_time := COALESCE(p_end_time, v_end_time);

    IF v_end_time <= v_start_time THEN
        RAISE EXCEPTION 'Schedule item end time must be after its start time' USING ERRCODE = '22023';
    END IF;

    SELECT start_date, end_date INTO v_event_start, v_event_end
    FROM events WHERE events_id = v_events_id;
    IF v_start_time < v_event_start OR v_end_time > v_event_end THEN
        RAISE EXCEPTION 'Schedule item must fall within the event time window' USING ERRCODE = '22023';
    END IF;

    IF EXISTS (
        SELECT 1 FROM schedule_items
        WHERE events_id = v_events_id
          AND schedule_items_id <> p_id
          AND v_start_time < end_time
          AND start_time < v_end_time
    ) THEN
        RAISE EXCEPTION 'Schedule item overlaps an existing item' USING ERRCODE = '23P01';
    END IF;

    UPDATE schedule_items SET
        title = v_title,
        type_category = v_type_category,
        start_time = v_start_time,
        end_time = v_end_time,
        updated_at = now()
    WHERE schedule_items_id = p_id;
END; $$;
