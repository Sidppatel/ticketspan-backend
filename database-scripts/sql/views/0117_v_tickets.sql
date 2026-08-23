CREATE OR REPLACE VIEW vw_tickets AS
SELECT
    bl.booking_lines_id AS ticket_id, bl.ticket_code, bl.qr_token, bl.seat_number,
    bl.status::text,
    bl.created_at,
    bl.invited_email, bl.invite_sent_at, bl.invite_expires_at, bl.claimed_at,
    bl.bookings_id,
    b.booking_number, b.status::text AS booking_status,
    bl.guest_users_id,
    gu.email AS guest_email,
    gu.first_name AS guest_first_name,
    gu.last_name AS guest_last_name,
    e.events_id AS events_id,
    e.title AS event_title,
    e.start_date AS event_start_date,
    e.end_date AS event_end_date,
    COALESCE(v.name, 'Online / Venue')::varchar(256) AS venue_name,
    COALESCE(addr.city, '') AS venue_city,
    b.users_id AS booking_user_id,
    bu.email AS booking_user_email,
    bl.invite_token_hash,
    bu.first_name AS booking_user_first_name,
    bu.last_name AS booking_user_last_name,
    (SELECT bl2.tables_id FROM booking_lines bl2
     WHERE bl2.bookings_id = b.bookings_id AND bl2.kind = 'Table' LIMIT 1) AS booking_table_id,
    COALESCE(ett.label, 'Ticket') AS ticket_type_label,
    e.slug AS event_slug
FROM booking_lines bl
JOIN bookings b ON bl.bookings_id = b.bookings_id
JOIN events e ON b.events_id = e.events_id
LEFT JOIN venues v ON e.venues_id = v.venues_id
LEFT JOIN addresses addr ON v.addresses_id = addr.addresses_id
LEFT JOIN users gu ON bl.guest_users_id = gu.users_id
JOIN users bu ON b.users_id = bu.users_id
LEFT JOIN event_ticket_types ett ON bl.event_ticket_types_id = ett.event_ticket_types_id
WHERE bl.kind = 'Ticket' AND b.status IN ('Paid', 'CheckedIn');
