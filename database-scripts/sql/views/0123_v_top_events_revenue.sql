CREATE OR REPLACE VIEW vw_top_events_revenue AS
SELECT
    e.events_id AS events_id,
    e.title AS title,
    COUNT(p.*)::int AS purchase_count,
    COALESCE(SUM(p.subtotal_cents)::bigint, 0) AS revenue_cents
FROM purchases p
JOIN events e ON e.events_id = p.events_id
WHERE p.status::text IN ('Paid','CheckedIn')
GROUP BY e.events_id, e.title
ORDER BY revenue_cents DESC
LIMIT 10;
