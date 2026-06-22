CREATE OR REPLACE VIEW vw_events_by_category AS
SELECT
    category::text AS category,
    COUNT(*)::int AS count
FROM events
GROUP BY category;
