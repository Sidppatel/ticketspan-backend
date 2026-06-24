CREATE OR REPLACE FUNCTION sp_get_next_event_dashboard(p_now timestamptz)
RETURNS TABLE (
    events_id               uuid,
    title                 text,
    slug                  text,
    status                text,
    category              text,
    start_date             timestamptz,
    end_date               timestamptz,
    venue_name             text,
    venue_address          text,
    venue_city             text,
    venue_state            text,
    image_path             text,
    layout_mode            text,
    days_until             int,
    total_bookings        int,
    paid_bookings         int,
    checked_in_bookings    int,
    pending_bookings      int,
    cancelled_bookings    int,
    refunded_bookings     int,
    revenue_cents          bigint,
    potential_revenue_cents bigint,
    total_capacity         int,
    sold_count             int
)
LANGUAGE plpgsql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_event_id uuid;
BEGIN
    SELECT e.events_id INTO v_event_id
    FROM events e
    WHERE e.status::text = 'Published' AND e.start_date > p_now
    ORDER BY e.start_date
    LIMIT 1;

    IF v_event_id IS NULL THEN
        SELECT e.events_id INTO v_event_id
        FROM events e
        WHERE e.start_date > p_now AND e.status::text != 'Cancelled'
        ORDER BY e.start_date
        LIMIT 1;
    END IF;

    IF v_event_id IS NULL THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT
        e.events_id                                          AS events_id,
        e.title::text                                 AS title,
        e.slug::text                                  AS slug,
        e.status::text                                AS status,
        COALESCE(e.category::text, '')                AS category,
        e.start_date                                   AS start_date,
        e.end_date                                     AS end_date,
        v.name::text                                  AS venue_name,
        COALESCE(addr.line1, '')::text                AS venue_address,
        COALESCE(addr.city, '')::text                 AS venue_city,
        COALESCE(addr.state, '')::text                AS venue_state,
        e.image_path::text                             AS image_path,
        e.layout_mode::text                            AS layout_mode,
        CEIL(EXTRACT(EPOCH FROM (e.start_date - p_now)) / 86400.0)::int AS days_until,
        ps.total_count                                  AS total_bookings,
        ps.paid_count                                   AS paid_bookings,
        ps.checkin_count                                AS checked_in_bookings,
        ps.pending_count                                AS pending_bookings,
        ps.cancelled_count                              AS cancelled_bookings,
        ps.refunded_count                               AS refunded_bookings,
        ps.revenue                                      AS revenue_cents,
        (CASE
            WHEN e.layout_mode::text = 'Open' AND ettp.min_price IS NOT NULL
                THEN ettp.capped_revenue
                     + GREATEST(COALESCE(e.max_capacity, ts.total_capacity) - ettp.capped_seats, 0)::bigint
                       * ettp.min_price::bigint
            ELSE ts.total_price::bigint
        END)::bigint                                    AS potential_revenue_cents,
        COALESCE(e.max_capacity, ts.total_capacity)    AS total_capacity,
        (ps.tickets_sold)                               AS sold_count
    FROM events e
    JOIN venues v ON v.venues_id = e.venues_id
    LEFT JOIN addresses addr ON v.addresses_id = addr.addresses_id
    CROSS JOIN LATERAL (
        SELECT
            COUNT(*)::int                                                                                  AS total_count,
            COUNT(*) FILTER (WHERE p.status::text = 'Paid')::int                                         AS paid_count,
            COUNT(*) FILTER (WHERE p.status::text = 'CheckedIn')::int                                    AS checkin_count,
            COUNT(*) FILTER (WHERE p.status::text = 'Pending')::int                                      AS pending_count,
            COUNT(*) FILTER (WHERE p.status::text = 'Cancelled')::int                                    AS cancelled_count,
            COUNT(*) FILTER (WHERE p.status::text = 'Refunded')::int                                     AS refunded_count,
            COALESCE(SUM(p.subtotal_cents) FILTER (WHERE p.status::text IN ('Paid','CheckedIn')), 0)::bigint AS revenue,
            COALESCE(SUM(COALESCE(p.seats_reserved, 1)) FILTER (WHERE p.status::text IN ('Paid','CheckedIn')), 0)::int AS tickets_sold
        FROM bookings p
        WHERE p.events_id = e.events_id
    ) ps
    CROSS JOIN LATERAL (
        SELECT
            COALESCE(SUM(et.capacity), 0)::int      AS total_capacity,
            COALESCE(SUM(et.price_cents::bigint), 0) AS total_price
        FROM tables t
        JOIN event_tables et ON et.event_tables_id = t.event_tables_id
        WHERE t.events_id = e.events_id AND t.is_active = true
    ) ts
    LEFT JOIN LATERAL (
        SELECT
            MIN(ett.price_cents)                                                                      AS min_price,
            COALESCE(SUM(COALESCE(ett.max_quantity, 0)::bigint * ett.price_cents::bigint), 0)::bigint AS capped_revenue,
            COALESCE(SUM(COALESCE(ett.max_quantity, 0)), 0)::int                                      AS capped_seats
        FROM event_ticket_types ett
        WHERE ett.events_id = e.events_id AND ett.is_active = true
    ) ettp ON true
    WHERE e.events_id = v_event_id;
END;
$$;
