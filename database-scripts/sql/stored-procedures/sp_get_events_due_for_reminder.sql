DROP FUNCTION IF EXISTS sp_get_events_due_for_reminder();

CREATE OR REPLACE FUNCTION sp_get_events_due_for_reminder()
RETURNS TABLE (
    events_id uuid,
    tenants_id uuid,
    title text,
    start_date timestamptz,
    venue_name text,
    venue_address text,
    tenant_slug text,
    reminder_type text,
    target_hours integer
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

    -- Reminder 1 Due (e.g. 168h / 7d or custom hours)
    RETURN QUERY
    SELECT e.events_id, e.tenants_id, e.title::text, e.start_date,
           COALESCE(v.name, 'Online / Venue')::text AS venue_name,
           COALESCE(CONCAT_WS(', ', a.line1, a.city, a.state), '')::text AS venue_address,
           COALESCE(t.slug, '')::text AS tenant_slug,
           '1'::text AS reminder_type,
           COALESCE(er.reminder_1_hours, v_def_1) AS target_hours
    FROM events e
    JOIN tenants t ON t.tenants_id = e.tenants_id
    LEFT JOIN venues v ON v.venues_id = e.venues_id
    LEFT JOIN addresses a ON a.addresses_id = v.addresses_id
    LEFT JOIN event_reminders er ON er.events_id = e.events_id
    WHERE e.status = 'Published'
      AND e.start_date >= now() + (COALESCE(er.reminder_1_hours, v_def_1) - 24 || ' hours')::interval
      AND e.start_date <= now() + (COALESCE(er.reminder_1_hours, v_def_1) + 2 || ' hours')::interval
      AND COALESCE(er.reminders_enabled, true) = true
      AND COALESCE(er.reminder_7d_sent, false) = false;

    -- Reminder 2 Due (e.g. 48h or custom hours)
    RETURN QUERY
    SELECT e.events_id, e.tenants_id, e.title::text, e.start_date,
           COALESCE(v.name, 'Online / Venue')::text AS venue_name,
           COALESCE(CONCAT_WS(', ', a.line1, a.city, a.state), '')::text AS venue_address,
           COALESCE(t.slug, '')::text AS tenant_slug,
           '2'::text AS reminder_type,
           COALESCE(er.reminder_2_hours, v_def_2) AS target_hours
    FROM events e
    JOIN tenants t ON t.tenants_id = e.tenants_id
    LEFT JOIN venues v ON v.venues_id = e.venues_id
    LEFT JOIN addresses a ON a.addresses_id = v.addresses_id
    LEFT JOIN event_reminders er ON er.events_id = e.events_id
    WHERE e.status = 'Published'
      AND e.start_date >= now()
      AND e.start_date <= now() + (COALESCE(er.reminder_2_hours, v_def_2) + 2 || ' hours')::interval
      AND COALESCE(er.reminders_enabled, true) = true
      AND COALESCE(er.reminder_48h_sent, false) = false;
END; $$;
