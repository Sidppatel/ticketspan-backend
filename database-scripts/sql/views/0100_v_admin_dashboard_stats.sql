CREATE OR REPLACE VIEW vw_admin_dashboard_stats AS
SELECT
    (SELECT COUNT(*)::int FROM events) AS total_events,
    (SELECT COUNT(*)::int FROM events WHERE status::text = 'Published') AS published_events,
    (SELECT COALESCE(SUM(COALESCE(seats_reserved, 1)), 0)::int FROM bookings WHERE status::text IN ('Paid','CheckedIn')) AS total_bookings,
    (SELECT COUNT(*)::int FROM bookings WHERE status::text = 'Paid') AS paid_bookings,
    (SELECT COUNT(*)::int FROM bookings WHERE status::text = 'CheckedIn') AS checked_in_bookings,
    COALESCE(
        (SELECT SUM(subtotal_cents)::bigint FROM bookings WHERE status::text IN ('Paid','CheckedIn')),
        0
    ) AS total_revenue_cents,
    (SELECT COUNT(*)::int FROM users) AS total_users,
    (SELECT COUNT(*)::int FROM venues) AS total_venues;
