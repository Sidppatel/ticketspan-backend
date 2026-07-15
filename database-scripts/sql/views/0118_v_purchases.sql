DROP VIEW IF EXISTS vw_bookings CASCADE;

CREATE VIEW vw_bookings AS
SELECT
    b.bookings_id AS bookings_id, b.booking_number, b.status::text,
    b.subtotal_cents, b.fee_cents, b.total_cents,
    b.tax_cents, (b.fee_cents - b.tax_cents) AS service_fee_cents,
    b.tax_rate, b.tax_state, b.tax_county, b.tax_city,
    b.qr_token, b.seats_reserved, b.created_at,
    b.users_id,
    u.email AS user_email,
    u.first_name AS user_first_name,
    u.last_name AS user_last_name,
    b.events_id,
    e.title AS event_title,
    e.slug AS event_slug,
    e.start_date AS event_start_date,
    e.end_date AS event_end_date,
    COALESCE(e.category::text, '') AS event_category,
    e.image_path AS event_image_path,
    e.fees_included AS fees_included,
    v.name AS venue_name,
    COALESCE(addr.line1, '') AS venue_address,
    COALESCE(addr.city, '') AS venue_city,
    COALESCE(addr.state, '') AS venue_state,
    COALESCE(addr.zip_code, '') AS venue_zip_code,
    COALESCE(st.paid_at, b.created_at) AS paid_at,
    st.stripe_transactions_id AS stripe_transaction_id,
    st.payment_intent_id,
    st.status::text AS payment_status,
    st.amount_cents AS payment_amount_cents,
    st.total_charged_cents,
    st.stripe_fees_cents,
    st.transfer_amount_cents,
    st.payment_method_type,
    st.payment_method_last4,
    st.payment_method_brand,
    e.created_by_users_id,
    COALESCE(pt_labels.labels, ARRAY[]::text[]) AS table_labels,
    COALESCE(tk.tickets_total, 0) AS tickets_total,
    COALESCE(tk.tickets_claimed, 0) AS tickets_claimed,
    COALESCE(tk.guest_search, '') AS guest_search
FROM bookings b
JOIN users u ON b.users_id = u.users_id
JOIN events e ON b.events_id = e.events_id
JOIN venues v ON e.venues_id = v.venues_id
LEFT JOIN addresses addr ON v.addresses_id = addr.addresses_id
LEFT JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
LEFT JOIN LATERAL (
    SELECT array_agg(t.label ORDER BY t.label) AS labels
    FROM booking_lines bl
    JOIN tables t ON t.tables_id = bl.tables_id
    WHERE bl.bookings_id = b.bookings_id AND bl.kind = 'Table'
) pt_labels ON true
LEFT JOIN LATERAL (
    SELECT
        COUNT(*) FILTER (WHERE bl.kind = 'Ticket')::int AS tickets_total,
        COUNT(*) FILTER (WHERE bl.kind = 'Ticket' AND bl.status IN ('Claimed', 'CheckedIn'))::int AS tickets_claimed,
        string_agg(
            COALESCE(bl.ticket_code, '') || ' ' || COALESCE(gu.email, '') || ' '
            || COALESCE(gu.first_name, '') || ' ' || COALESCE(gu.last_name, ''), ' ') AS guest_search
    FROM booking_lines bl
    LEFT JOIN users gu ON gu.users_id = bl.guest_users_id
    WHERE bl.bookings_id = b.bookings_id
) tk ON true;