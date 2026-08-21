DROP FUNCTION IF EXISTS sp_get_event_reminder_settings(uuid);

CREATE OR REPLACE FUNCTION sp_get_event_reminder_settings(
    p_events_id uuid
) RETURNS TABLE (
    events_id uuid,
    reminders_enabled boolean,
    reminder_1_hours integer,
    reminder_2_hours integer,
    default_reminder_1_hours integer,
    default_reminder_2_hours integer,
    reminder_7d_sent boolean,
    reminder_48h_sent boolean,
    last_manual_reminder_at timestamptz,
    manual_reminder_count integer
) LANGUAGE plpgsql
    SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
#variable_conflict use_column
DECLARE
    v_val text;
    v_def_1 integer := 168;
    v_def_2 integer := 48;
BEGIN
    SELECT s.value INTO v_val FROM app_settings s WHERE s.key = 'event_reminder';
    IF v_val IS NOT NULL THEN
        BEGIN
            v_def_1 := COALESCE((v_val::jsonb->>0)::int, 168);
            v_def_2 := COALESCE((v_val::jsonb->>1)::int, 48);
        EXCEPTION WHEN OTHERS THEN
            v_def_1 := 168;
            v_def_2 := 48;
        END;
    END IF;

    -- Ensure row exists with default enabled = true
    INSERT INTO event_reminders (events_id, reminders_enabled, reminder_1_hours, reminder_2_hours, reminder_7d_sent, reminder_48h_sent, manual_reminder_count, created_at, updated_at)
    VALUES (p_events_id, true, NULL, NULL, false, false, 0, now(), now())
    ON CONFLICT ON CONSTRAINT event_reminders_pkey DO NOTHING;

    RETURN QUERY
    SELECT er.events_id,
           er.reminders_enabled,
           COALESCE(er.reminder_1_hours, v_def_1) AS reminder_1_hours,
           COALESCE(er.reminder_2_hours, v_def_2) AS reminder_2_hours,
           v_def_1 AS default_reminder_1_hours,
           v_def_2 AS default_reminder_2_hours,
           er.reminder_7d_sent,
           er.reminder_48h_sent,
           er.last_manual_reminder_at,
           er.manual_reminder_count
    FROM event_reminders er
    WHERE er.events_id = p_events_id;
END; $$;
