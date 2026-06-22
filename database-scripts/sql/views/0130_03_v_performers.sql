CREATE OR REPLACE VIEW vw_performers AS
SELECT
    p.performers_id AS performers_id,
    p.name AS name,
    p.slug AS slug,
    p.primary_image_path AS primary_image_path,
    p.meta AS meta,
    COALESCE(ec.total, 0)::int AS event_count,
    COALESCE(ec.upcoming, 0)::int AS upcoming_event_count,
    p.created_at AS created_at,
    p.updated_at AS updated_at
FROM performers p
LEFT JOIN LATERAL (
    SELECT
        COUNT(*)::int AS total,
        COUNT(*) FILTER (
            WHERE e.start_date >= now()
              AND e.status = 'Published'
        )::int AS upcoming
    FROM event_performers ep
    JOIN events e ON e.events_id = ep.events_id
    WHERE ep.performers_id = p.performers_id
) ec ON true;
