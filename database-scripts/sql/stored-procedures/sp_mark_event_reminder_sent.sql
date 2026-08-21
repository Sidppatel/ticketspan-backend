CREATE OR REPLACE FUNCTION sp_mark_event_reminder_sent(
    p_events_id uuid,
    p_type text
) RETURNS boolean LANGUAGE plpgsql
    SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
#variable_conflict use_column
BEGIN
    INSERT INTO event_reminders (events_id, reminders_enabled, reminder_1_hours, reminder_2_hours, reminder_7d_sent, reminder_48h_sent, manual_reminder_count, created_at, updated_at)
    VALUES (p_events_id, true, NULL, NULL, false, false, 0, now(), now())
    ON CONFLICT ON CONSTRAINT event_reminders_pkey DO NOTHING;

    IF p_type = '1' OR p_type = '7d' THEN
        UPDATE event_reminders
        SET reminder_7d_sent = true,
            updated_at = now()
        WHERE event_reminders.events_id = p_events_id;
    ELSIF p_type = '2' OR p_type = '48h' THEN
        UPDATE event_reminders
        SET reminder_48h_sent = true,
            updated_at = now()
        WHERE event_reminders.events_id = p_events_id;
    ELSIF p_type = 'manual' THEN
        UPDATE event_reminders
        SET last_manual_reminder_at = now(),
            manual_reminder_count = event_reminders.manual_reminder_count + 1,
            updated_at = now()
        WHERE event_reminders.events_id = p_events_id;
    END IF;

    RETURN true;
END; $$;
