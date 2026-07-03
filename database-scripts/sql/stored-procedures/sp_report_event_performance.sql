DROP FUNCTION IF EXISTS sp_report_event_performance(timestamptz, timestamptz);

CREATE OR REPLACE FUNCTION sp_report_event_performance(p_from timestamptz, p_to timestamptz)
RETURNS TABLE (
    events_id            uuid,
    event_title          text,
    event_start_date     timestamptz,
    event_status         text,
    revenue_cents        bigint,
    orders               int,
    tickets_sold         int,
    checked_in           int,
    capacity             int,
    capacity_used_bps    int,
    attendance_rate_bps  int,
    revenue_per_attendee_cents bigint,
    refunded_cents       bigint,
    refunded_orders      int,
    first_sale_at        timestamptz,
    last_sale_at         timestamptz,
    sales_per_day_milli  int
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    WITH sold AS (
        SELECT
            b.events_id,
            b.status::text AS status,
            b.subtotal_cents,
            COALESCE(b.seats_reserved, 1) AS seats,
            COALESCE(st.paid_at, b.created_at) AS sold_at
        FROM bookings b
        LEFT JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
        WHERE b.status::text IN ('Paid','CheckedIn','Refunded')
          AND COALESCE(st.paid_at, b.created_at) >= p_from
          AND COALESCE(st.paid_at, b.created_at) <  p_to
    ),
    per_event AS (
        SELECT
            s.events_id,
            SUM(s.subtotal_cents) FILTER (WHERE s.status IN ('Paid','CheckedIn'))       AS revenue_cents,
            COUNT(*) FILTER (WHERE s.status IN ('Paid','CheckedIn'))                    AS orders,
            SUM(s.seats) FILTER (WHERE s.status IN ('Paid','CheckedIn'))                AS tickets_sold,
            SUM(s.seats) FILTER (WHERE s.status = 'CheckedIn')                          AS checked_in,
            SUM(s.subtotal_cents) FILTER (WHERE s.status = 'Refunded')                  AS refunded_cents,
            COUNT(*) FILTER (WHERE s.status = 'Refunded')                               AS refunded_orders,
            MIN(s.sold_at) FILTER (WHERE s.status IN ('Paid','CheckedIn'))              AS first_sale_at,
            MAX(s.sold_at) FILTER (WHERE s.status IN ('Paid','CheckedIn'))              AS last_sale_at
        FROM sold s
        GROUP BY s.events_id
    ),
    caps AS (
        SELECT ett.events_id, SUM(ett.capacity)::int AS capacity
        FROM event_ticket_types ett
        WHERE ett.capacity IS NOT NULL
        GROUP BY ett.events_id
    )
    SELECT
        e.events_id,
        e.title::text,
        e.start_date,
        e.status::text,
        COALESCE(pe.revenue_cents, 0)::bigint,
        COALESCE(pe.orders, 0)::int,
        COALESCE(pe.tickets_sold, 0)::int,
        COALESCE(pe.checked_in, 0)::int,
        COALESCE(c.capacity, 0)::int,
        CASE WHEN COALESCE(c.capacity, 0) = 0 THEN 0
             ELSE (COALESCE(pe.tickets_sold, 0) * 10000 / c.capacity)::int END,
        CASE WHEN COALESCE(pe.tickets_sold, 0) = 0 THEN 0
             ELSE (COALESCE(pe.checked_in, 0) * 10000 / pe.tickets_sold)::int END,
        CASE WHEN COALESCE(pe.tickets_sold, 0) = 0 THEN 0
             ELSE (pe.revenue_cents / pe.tickets_sold)::bigint END,
        COALESCE(pe.refunded_cents, 0)::bigint,
        COALESCE(pe.refunded_orders, 0)::int,
        pe.first_sale_at,
        pe.last_sale_at,
        CASE WHEN pe.first_sale_at IS NULL OR pe.last_sale_at <= pe.first_sale_at THEN COALESCE(pe.tickets_sold, 0) * 1000
             ELSE (pe.tickets_sold * 1000.0 /
                   GREATEST(EXTRACT(EPOCH FROM (pe.last_sale_at - pe.first_sale_at)) / 86400.0, 1.0))::int END
    FROM events e
    LEFT JOIN per_event pe ON pe.events_id = e.events_id
    LEFT JOIN caps c ON c.events_id = e.events_id
    WHERE pe.events_id IS NOT NULL
       OR (e.start_date >= p_from AND e.start_date < p_to)
    ORDER BY COALESCE(pe.revenue_cents, 0) DESC;
$$;
