CREATE OR REPLACE VIEW vw_sponsors AS
SELECT
    s.sponsors_id AS sponsors_id,
    s.name AS name,
    s.slug AS slug,
    s.primary_image_path AS primary_image_path,
    s.meta AS meta,
    COALESCE(ec.total, 0)::int AS event_count,
    COALESCE(ec.upcoming, 0)::int AS upcoming_event_count,
    s.created_at AS created_at,
    s.updated_at AS updated_at,
    s.is_active AS is_active
FROM sponsors s
LEFT JOIN LATERAL (
    SELECT
        COUNT(*)::int AS total,
        COUNT(*) FILTER (
            WHERE e.start_date >= now()
              AND e.status = 'Published'
        )::int AS upcoming
    FROM event_sponsors es
    JOIN events e ON e.events_id = es.events_id
    WHERE es.sponsors_id = s.sponsors_id
) ec ON true;
