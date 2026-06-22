CREATE OR REPLACE FUNCTION sp_events_by_performer(
    p_performer_id uuid,
    p_status text,
    p_offset int,
    p_limit int
) RETURNS SETOF vw_events LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT v.*
    FROM vw_events v
    JOIN event_performers ep ON ep.events_id = v.events_id
    WHERE ep.performers_id = p_performer_id
      AND (p_status IS NULL OR v.status = p_status)
    ORDER BY v.start_date DESC
    OFFSET COALESCE(p_offset, 0)
    LIMIT LEAST(COALESCE(p_limit, 20), 100);
$$;
