CREATE OR REPLACE VIEW vw_bookings_by_status AS
SELECT
    status::text AS status,
    COUNT(*)::int AS count
FROM bookings
GROUP BY status;
