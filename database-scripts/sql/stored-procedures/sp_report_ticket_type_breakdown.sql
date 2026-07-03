DROP FUNCTION IF EXISTS sp_report_ticket_type_breakdown(timestamptz, timestamptz);

CREATE OR REPLACE FUNCTION sp_report_ticket_type_breakdown(p_from timestamptz, p_to timestamptz)
RETURNS TABLE (
    event_ticket_types_id uuid,
    label                 text,
    events_id             uuid,
    event_title           text,
    price_cents           bigint,
    quantity_sold         int,
    revenue_cents         bigint,
    refunded_quantity     int,
    refunded_cents        bigint
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        bl.event_ticket_types_id,
        COALESCE(ett.label, 'Tables')::text AS label,
        e.events_id,
        e.title::text,
        COALESCE(ett.price_cents, 0)::bigint,
        COALESCE(SUM(bl.seats) FILTER (WHERE b.status::text IN ('Paid','CheckedIn')), 0)::int,
        COALESCE(SUM(bl.selling_price_cents) FILTER (WHERE b.status::text IN ('Paid','CheckedIn')), 0)::bigint,
        COALESCE(SUM(bl.seats) FILTER (WHERE b.status::text = 'Refunded'), 0)::int,
        COALESCE(SUM(bl.selling_price_cents) FILTER (WHERE b.status::text = 'Refunded'), 0)::bigint
    FROM booking_lines bl
    JOIN bookings b ON b.bookings_id = bl.bookings_id
    JOIN events e ON e.events_id = COALESCE(bl.events_id, b.events_id)
    LEFT JOIN event_ticket_types ett ON ett.event_ticket_types_id = bl.event_ticket_types_id
    LEFT JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
    WHERE b.status::text IN ('Paid','CheckedIn','Refunded')
      AND COALESCE(st.paid_at, b.created_at) >= p_from
      AND COALESCE(st.paid_at, b.created_at) <  p_to
    GROUP BY bl.event_ticket_types_id, ett.label, e.events_id, e.title, ett.price_cents
    ORDER BY 7 DESC;
$$;
