DROP FUNCTION IF EXISTS sp_report_sales_by_channel(timestamptz, timestamptz);

CREATE OR REPLACE FUNCTION sp_report_sales_by_channel(p_from timestamptz, p_to timestamptz)
RETURNS TABLE (
    channel       text,
    orders        int,
    tickets_sold  int,
    revenue_cents bigint
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        b.sales_channel::text,
        COUNT(*)::int,
        COALESCE(SUM(COALESCE(b.seats_reserved, 1)), 0)::int,
        COALESCE(SUM(b.subtotal_cents), 0)::bigint
    FROM bookings b
    LEFT JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
    WHERE b.status::text IN ('Paid','CheckedIn')
      AND COALESCE(st.paid_at, b.created_at) >= p_from
      AND COALESCE(st.paid_at, b.created_at) <  p_to
    GROUP BY b.sales_channel
    ORDER BY 4 DESC;
$$;
