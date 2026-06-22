CREATE OR REPLACE VIEW vw_admin_dashboard_stats AS
SELECT
    (SELECT COUNT(*)::int FROM events) AS total_events,
    (SELECT COUNT(*)::int FROM events WHERE status::text = 'Published') AS published_events,
    (SELECT COALESCE(SUM(COALESCE(seats_reserved, 1)), 0)::int FROM purchases WHERE status::text IN ('Paid','CheckedIn')) AS total_purchases,
    (SELECT COUNT(*)::int FROM purchases WHERE status::text = 'Paid') AS paid_purchases,
    (SELECT COUNT(*)::int FROM purchases WHERE status::text = 'CheckedIn') AS checked_in_purchases,
    COALESCE(
        (SELECT SUM(subtotal_cents)::bigint FROM purchases WHERE status::text IN ('Paid','CheckedIn')),
        0
    ) AS total_revenue_cents,
    (SELECT COUNT(*)::int FROM users) AS total_users,
    (SELECT COUNT(*)::int FROM venues) AS total_venues;
