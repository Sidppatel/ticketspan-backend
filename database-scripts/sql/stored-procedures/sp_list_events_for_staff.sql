CREATE OR REPLACE FUNCTION sp_list_events_for_staff(
    p_business_user_id uuid, p_grace_hours int DEFAULT 24
) RETURNS SETOF events LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT e.*
    FROM events e
    JOIN staff_event_access aue ON aue.event_id = e.events_id
    WHERE aue.staff_user_id = p_business_user_id
      AND e.status IN ('Published', 'Completed')
      AND now() >= COALESCE(aue.access_start, e.start_date - make_interval(hours => p_grace_hours))
      AND now() <= COALESCE(aue.access_end, e.end_date + make_interval(hours => p_grace_hours))
    ORDER BY e.start_date;
$$;
