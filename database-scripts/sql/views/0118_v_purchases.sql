CREATE OR REPLACE VIEW vw_bookings AS
SELECT
    b.bookings_id AS bookings_id, b.booking_number, b.status::text,
    b.subtotal_cents, b.fee_cents, b.total_cents,
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
    v.name AS venue_name,
    COALESCE(addr.line1, '') AS venue_address,
    COALESCE(addr.city, '') AS venue_city,
    COALESCE(addr.state, '') AS venue_state,
    b.tables_id,
    tbl.label AS table_label,
    b.event_ticket_types_id,
    ett.label AS event_ticket_type_label,
    st.stripe_transactions_id AS stripe_transaction_id,
    st.payment_intent_id,
    st.tax_calculation_id,
    st.tax_transaction_id,
    st.status::text AS payment_status,
    st.amount_cents AS payment_amount_cents,
    st.total_charged_cents,
    st.tax_amount_cents,
    st.stripe_fees_cents,
    st.transfer_amount_cents,
    st.paid_at, st.refunded_at,
    COALESCE(tc.cnt, 0)::int AS ticket_count,
    e.created_by_users_id,
    COALESCE(pt_labels.labels, ARRAY[]::text[]) AS table_labels
FROM bookings b
JOIN users u ON b.users_id = u.users_id
JOIN events e ON b.events_id = e.events_id
JOIN venues v ON e.venues_id = v.venues_id
LEFT JOIN addresses addr ON v.addresses_id = addr.addresses_id
LEFT JOIN tables tbl ON b.tables_id = tbl.tables_id
LEFT JOIN event_ticket_types ett ON b.event_ticket_types_id = ett.event_ticket_types_id
LEFT JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM tickets bt WHERE bt.bookings_id = b.bookings_id
) tc ON true
LEFT JOIN LATERAL (
    SELECT array_agg(t.label ORDER BY t.label) AS labels
    FROM booking_tables pt
    JOIN tables t ON t.tables_id = pt.tables_id
    WHERE pt.bookings_id = b.bookings_id
) pt_labels ON true;