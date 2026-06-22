CREATE OR REPLACE VIEW vw_purchases_by_status AS
SELECT
    status::text AS status,
    COUNT(*)::int AS count
FROM purchases
GROUP BY status;
