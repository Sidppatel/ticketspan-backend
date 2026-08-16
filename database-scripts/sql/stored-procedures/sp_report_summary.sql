DROP FUNCTION IF EXISTS sp_report_summary(timestamptz, timestamptz);

CREATE OR REPLACE FUNCTION sp_report_summary(p_from timestamptz, p_to timestamptz)
RETURNS TABLE (
    revenue_cents       bigint,
    orders              int,
    tickets_sold        int,
    average_order_cents bigint,
    visits              int,
    conversion_bps      int,
    refunded_cents      bigint,
    refunded_orders     int,
    service_fee_cents   bigint,
    tax_cents           bigint,
    net_revenue_cents   bigint
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    WITH sales AS (
        SELECT 
            b.subtotal_cents,
            COALESCE(b.fee_cents, 0) AS fee_cents,
            COALESCE(b.tax_cents, 0) AS tax_cents,
            COALESCE(b.seats_reserved, 1) AS seats,
            b.status::text AS status
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
        COALESCE(SUM(p.subtotal_cents), 0)::bigint AS revenue_cents,
        COUNT(p.*)::int AS orders,
        COALESCE(SUM(p.seats), 0)::int AS tickets_sold,
        CASE WHEN COUNT(p.*) = 0 THEN 0 ELSE (SUM(p.subtotal_cents) / COUNT(p.*))::bigint END AS average_order_cents,
        pv.visits,
        CASE WHEN pv.visits = 0 THEN 0 ELSE (COUNT(p.*) * 10000 / pv.visits)::int END AS conversion_bps,
        COALESCE((SELECT SUM(r.subtotal_cents) FROM refunded r), 0)::bigint AS refunded_cents,
        COALESCE((SELECT COUNT(*) FROM refunded r), 0)::int AS refunded_orders,
        COALESCE(SUM(p.fee_cents), 0)::bigint AS service_fee_cents,
        COALESCE(SUM(p.tax_cents), 0)::bigint AS tax_cents,
        (COALESCE(SUM(p.subtotal_cents), 0) - COALESCE((SELECT SUM(r.subtotal_cents) FROM refunded r), 0))::bigint AS net_revenue_cents
    FROM page_views pv
    LEFT JOIN paid p ON true
    GROUP BY pv.visits;
$$;
