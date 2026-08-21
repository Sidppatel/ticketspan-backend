CREATE OR REPLACE FUNCTION sp_set_event_reminder_settings(
    p_events_id uuid,
    p_enabled boolean,
    p_reminder_1_hours integer DEFAULT NULL,
    p_reminder_2_hours integer DEFAULT NULL
) RETURNS boolean LANGUAGE plpgsql
    SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
#variable_conflict use_column
BEGIN
    INSERT INTO event_reminders (events_id, reminders_enabled, reminder_1_hours, reminder_2_hours, reminder_7d_sent, reminder_48h_sent, manual_reminder_count, created_at, updated_at)
    VALUES (p_events_id, p_enabled, p_reminder_1_hours, p_reminder_2_hours, false, false, 0, now(), now())
    ON CONFLICT ON CONSTRAINT event_reminders_pkey DO UPDATE
    SET reminders_enabled = p_enabled,
        reminder_1_hours = CASE WHEN p_reminder_1_hours IS NOT NULL THEN p_reminder_1_hours ELSE event_reminders.reminder_1_hours END,
        reminder_2_hours = CASE WHEN p_reminder_2_hours IS NOT NULL THEN p_reminder_2_hours ELSE event_reminders.reminder_2_hours END,
        updated_at = now();

    RETURN true;
END; $$;
