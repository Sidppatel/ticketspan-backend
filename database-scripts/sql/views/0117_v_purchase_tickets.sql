CREATE OR REPLACE VIEW vw_purchase_tickets AS
SELECT
    bt.purchase_tickets_id AS purchase_ticket_id, bt.ticket_code, bt.qr_token, bt.seat_number,
    bt.status::text,
    bt.created_at,
    bt.invited_email, bt.invite_sent_at, bt.invite_expires_at, bt.claimed_at,
    bt.purchases_id,
    b.purchase_number, b.status::text AS purchase_status,
    bt.guest_users_id,
    gu.email AS guest_email,
    gu.first_name AS guest_first_name,
    gu.last_name AS guest_last_name,
    e.events_id AS events_id,
    e.title AS event_title,
    e.start_date AS event_start_date,
    e.end_date AS event_end_date,
    v.name AS venue_name,
    COALESCE(addr.city, '') AS venue_city,
    b.users_id AS purchase_user_id,
    bu.email AS purchase_user_email,
    bt.invite_token_hash,
    bu.first_name AS purchase_user_first_name,
    bu.last_name AS purchase_user_last_name,
    b.tables_id AS purchase_table_id
FROM purchase_tickets bt
JOIN purchases b ON bt.purchases_id = b.purchases_id
JOIN events e ON b.events_id = e.events_id
JOIN venues v ON e.venues_id = v.venues_id
LEFT JOIN addresses addr ON v.addresses_id = addr.addresses_id
LEFT JOIN users gu ON bt.guest_users_id = gu.users_id
JOIN users bu ON b.users_id = bu.users_id;
