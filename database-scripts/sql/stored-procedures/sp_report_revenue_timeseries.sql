DROP FUNCTION IF EXISTS sp_report_revenue_timeseries(timestamptz, timestamptz, text);

CREATE OR REPLACE FUNCTION sp_report_revenue_timeseries(p_from timestamptz, p_to timestamptz, p_bucket text)
RETURNS TABLE (
    bucket_start  timestamptz,
    revenue_cents bigint,
    orders        int,
    tickets_sold  int
)
LANGUAGE plpgsql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    IF p_bucket NOT IN ('day','week','month','year') THEN
        RAISE EXCEPTION 'invalid bucket: %', p_bucket;
    END IF;
    RETURN QUERY
    SELECT
        date_trunc(p_bucket, COALESCE(st.paid_at, b.created_at)) AS bucket_start,
        COALESCE(SUM(b.subtotal_cents), 0)::bigint,
        COUNT(*)::int,
        COALESCE(SUM(COALESCE(b.seats_reserved, 1)), 0)::int
    FROM bookings b
    LEFT JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
    WHERE b.status::text IN ('Paid','CheckedIn')
      AND COALESCE(st.paid_at, b.created_at) >= p_from
      AND COALESCE(st.paid_at, b.created_at) <  p_to
    GROUP BY 1
    ORDER BY 1;
END; $$;
