DROP FUNCTION IF EXISTS sp_report_ticket_type_breakdown(timestamptz, timestamptz);

CREATE OR REPLACE FUNCTION sp_report_ticket_type_breakdown(p_from timestamptz, p_to timestamptz)
RETURNS TABLE (
    event_ticket_types_id text,
    label                 text,
    events_id             uuid,
    event_title           text,
    price_cents           bigint,
    quantity_sold         int,
    revenue_cents         bigint,
    refunded_quantity     int,
    refunded_cents        bigint,
    item_kind             text
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    WITH ticket_rows AS (
        SELECT
            COALESCE(bl.event_ticket_types_id::text, '')::text AS event_ticket_types_id,
            COALESCE(ett.label, 'Ticket')::text AS label,
            e.events_id,
            e.title::text AS event_title,
            COALESCE(ett.price_cents, bl.base_price_cents, 0)::bigint AS price_cents,
            COALESCE(SUM(bl.seats) FILTER (WHERE b.status::text IN ('Paid','CheckedIn')), 0)::int AS quantity_sold,
            COALESCE(SUM(bl.selling_price_cents) FILTER (WHERE b.status::text IN ('Paid','CheckedIn')), 0)::bigint AS revenue_cents,
            COALESCE(SUM(bl.seats) FILTER (WHERE b.status::text = 'Refunded'), 0)::int AS refunded_quantity,
            COALESCE(SUM(bl.selling_price_cents) FILTER (WHERE b.status::text = 'Refunded'), 0)::bigint AS refunded_cents,
            'ticket'::text AS item_kind
        FROM booking_lines bl
        JOIN bookings b ON b.bookings_id = bl.bookings_id
        JOIN events e ON e.events_id = COALESCE(bl.events_id, b.events_id)
        LEFT JOIN event_ticket_types ett ON ett.event_ticket_types_id = bl.event_ticket_types_id
        LEFT JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
        WHERE bl.kind = 'Ticket'
          AND b.status::text IN ('Paid','CheckedIn','Refunded')
          AND COALESCE(st.paid_at, b.created_at) >= p_from
          AND COALESCE(st.paid_at, b.created_at) <  p_to
        GROUP BY bl.event_ticket_types_id, ett.label, e.events_id, e.title, ett.price_cents, bl.base_price_cents
    ),
    table_rows AS (
        SELECT
            COALESCE(et.event_tables_id::text, t.tables_id::text, '')::text AS event_ticket_types_id,
            COALESCE(et.label, t.label, 'Table')::text AS label,
            e.events_id,
            e.title::text AS event_title,
            COALESCE(et.price_cents, bl.base_price_cents, 0)::bigint AS price_cents,
            COALESCE(SUM(bl.seats) FILTER (WHERE b.status::text IN ('Paid','CheckedIn')), 0)::int AS quantity_sold,
            COALESCE(SUM(bl.selling_price_cents) FILTER (WHERE b.status::text IN ('Paid','CheckedIn')), 0)::bigint AS revenue_cents,
            COALESCE(SUM(bl.seats) FILTER (WHERE b.status::text = 'Refunded'), 0)::int AS refunded_quantity,
            COALESCE(SUM(bl.selling_price_cents) FILTER (WHERE b.status::text = 'Refunded'), 0)::bigint AS refunded_cents,
            'table'::text AS item_kind
        FROM booking_lines bl
        JOIN bookings b ON b.bookings_id = bl.bookings_id
        JOIN events e ON e.events_id = COALESCE(bl.events_id, b.events_id)
        LEFT JOIN tables t ON t.tables_id = bl.tables_id
        LEFT JOIN event_tables et ON et.event_tables_id = t.event_tables_id
        LEFT JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
        WHERE bl.kind = 'Table'
          AND b.status::text IN ('Paid','CheckedIn','Refunded')
          AND COALESCE(st.paid_at, b.created_at) >= p_from
          AND COALESCE(st.paid_at, b.created_at) <  p_to
        GROUP BY et.event_tables_id, t.tables_id, et.label, t.label, e.events_id, e.title, et.price_cents, bl.base_price_cents
    )
    SELECT * FROM ticket_rows
    UNION ALL
    SELECT * FROM table_rows
    ORDER BY revenue_cents DESC;
$$;
