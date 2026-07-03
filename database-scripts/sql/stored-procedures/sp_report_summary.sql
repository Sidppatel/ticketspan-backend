DROP FUNCTION IF EXISTS sp_report_summary(timestamptz, timestamptz);

CREATE OR REPLACE FUNCTION sp_report_summary(p_from timestamptz, p_to timestamptz)
RETURNS TABLE (
    revenue_cents      bigint,
    orders             int,
    tickets_sold       int,
    average_order_cents bigint,
    visits             int,
    conversion_bps     int,
    refunded_cents     bigint,
    refunded_orders    int
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    WITH sales AS (
        SELECT b.subtotal_cents, COALESCE(b.seats_reserved, 1) AS seats, b.status::text AS status
        FROM bookings b
        LEFT JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
        WHERE b.status::text IN ('Paid','CheckedIn','Refunded')
          AND COALESCE(st.paid_at, b.created_at) >= p_from
          AND COALESCE(st.paid_at, b.created_at) <  p_to
    ),
    paid AS (SELECT * FROM sales WHERE status IN ('Paid','CheckedIn')),
    refunded AS (SELECT * FROM sales WHERE status = 'Refunded'),
    page_views AS (
        SELECT COUNT(*)::int AS visits
        FROM audit_logs
        WHERE event_type = 'PageView' AND created_at >= p_from AND created_at < p_to
    )
    SELECT
        COALESCE(SUM(p.subtotal_cents), 0)::bigint,
        COUNT(p.*)::int,
        COALESCE(SUM(p.seats), 0)::int,
        CASE WHEN COUNT(p.*) = 0 THEN 0 ELSE (SUM(p.subtotal_cents) / COUNT(p.*))::bigint END,
        pv.visits,
        CASE WHEN pv.visits = 0 THEN 0 ELSE (COUNT(p.*) * 10000 / pv.visits)::int END,
        (SELECT COALESCE(SUM(r.subtotal_cents), 0)::bigint FROM refunded r),
        (SELECT COUNT(*)::int FROM refunded r)
    FROM page_views pv
    LEFT JOIN paid p ON true
    GROUP BY pv.visits;
$$;
