DROP FUNCTION IF EXISTS sp_list_events_for_staff(uuid, int);
CREATE OR REPLACE FUNCTION sp_list_events_for_staff(
    p_business_user_id uuid, p_grace_hours int DEFAULT 24
) RETURNS TABLE(
    events_id uuid,
    title text,
    slug text,
    start_date timestamptz,
    end_date timestamptz,
    status text,
    venue_name text
) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT e.events_id, e.title, e.slug, e.start_date, e.end_date, e.status::text, v.name AS venue_name
    FROM events e
    JOIN staff_event_access aue ON aue.event_id = e.events_id
    LEFT JOIN venues v ON v.venues_id = e.venues_id
    WHERE aue.staff_user_id = p_business_user_id
      AND e.status IN ('Published', 'Completed')
      AND now() >= COALESCE(aue.access_start, e.start_date - make_interval(hours => p_grace_hours))
      AND now() <= COALESCE(aue.access_end, e.end_date + make_interval(hours => p_grace_hours))
    ORDER BY e.start_date;
$$;
