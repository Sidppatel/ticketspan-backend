CREATE OR REPLACE FUNCTION sp_event_stats(p_event_id uuid)
RETURNS TABLE(
    total_sold int,
    max_capacity int,
    fill_rate_pct int,
    gross_revenue_cents bigint
) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        COALESCE(v.total_sold, 0)::int AS total_sold,
        COALESCE(v.total_capacity, 0)::int AS max_capacity,
        CASE WHEN COALESCE(v.total_capacity, 0) > 0
            THEN ((COALESCE(v.total_sold, 0)::numeric / v.total_capacity::numeric) * 100)::int
            ELSE 0 END AS fill_rate_pct,
        COALESCE((
            SELECT SUM(b.subtotal_cents)::bigint
            FROM bookings b
            WHERE b.events_id = p_event_id
              AND b.status IN ('Paid', 'CheckedIn')
        ), 0) AS gross_revenue_cents
    FROM vw_events v
    WHERE v.events_id = p_event_id;
$$;