DROP VIEW IF EXISTS vw_events CASCADE;
CREATE OR REPLACE VIEW vw_events AS
SELECT
    e.events_id AS events_id,
    e.title AS title,
    e.slug AS slug,
    e.description AS description,
    e.status::text AS status,
    COALESCE(e.category::text, '') AS category,
    e.start_date AS start_date,
    e.end_date AS end_date,
    e.image_path AS image_path,
    e.is_featured AS is_featured,
    e.layout_mode::text AS layout_mode,
    e.max_capacity AS max_capacity,
    ettp.min_price::int AS price_per_person_cents,
    e.grid_rows AS grid_rows,
    e.grid_cols AS grid_cols,
    e.published_at AS published_at,
    e.scheduled_publish_at AS scheduled_publish_at,
    e.venues_id AS venues_id,
    e.created_by_users_id AS users_id,
    e.created_at AS created_at,
    e.updated_at AS updated_at,
    v.name AS venue_name,
    COALESCE(a.line1, '') AS venue_address,
    COALESCE(a.city, '') AS venue_city,
    COALESCE(a.state, '') AS venue_state,
    COALESCE(a.zip_code, '') AS venue_zip_code,
    v.description AS venue_description,
    v.image_path AS venue_image_path,
    v.phone AS venue_phone,
    v.email AS venue_email,
    v.website AS venue_website,
    v.is_active AS venue_is_active,
    v.created_at AS venue_created_at,
    COALESCE(au.first_name, '') AS organizer_first_name,
    COALESCE(au.last_name, '') AS organizer_last_name,
    COALESCE(
        e.max_capacity,
        CASE
            WHEN e.layout_mode::text = 'Grid' THEN table_cap.total_seats
            ELSE ett_cap.total_qty
        END,
        0
    )::int AS total_capacity,
    COALESCE(bs.sold, 0)::int AS total_sold,
    COALESCE(ts.available, 0)::int AS available_tables,
    ts.min_price::int AS min_table_price_cents,
    ettp.min_price::int AS min_ticket_type_price_cents,
    ts.min_total_price::int AS display_min_table_price_cents,
    ettp.min_total_price::int AS display_min_ticket_type_price_cents
FROM events e
JOIN venues v ON e.venues_id = v.venues_id
LEFT JOIN addresses a ON v.addresses_id = a.addresses_id
LEFT JOIN users au ON e.created_by_users_id = au.users_id
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(b.seats_reserved), COUNT(*))::int AS sold
    FROM bookings b
    WHERE b.events_id = e.events_id AND b.status IN ('Paid','CheckedIn')
) bs ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS available, MIN(et.price_cents) AS min_price, MIN(et.price_cents + COALESCE(et.platform_fee_cents, 0)) AS min_total_price
    FROM tables t
    JOIN event_tables et ON t.event_tables_id = et.event_tables_id
    WHERE t.events_id = e.events_id AND t.is_active = true AND t.status = 'Available'
) ts ON true
LEFT JOIN LATERAL (
    SELECT MIN(ett.price_cents) AS min_price, MIN(ett.price_cents + COALESCE(ett.platform_fee_cents, 0)) AS min_total_price
    FROM event_ticket_types ett
    WHERE ett.events_id = e.events_id AND ett.is_active = true
) ettp ON true
LEFT JOIN LATERAL (
    SELECT SUM(ett.max_quantity) AS total_qty
    FROM event_ticket_types ett
    WHERE ett.events_id = e.events_id AND ett.is_active = true
) ett_cap ON true
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(et.capacity), 0)::int AS total_seats
    FROM tables t
    JOIN event_tables et ON t.event_tables_id = et.event_tables_id
    WHERE t.events_id = e.events_id AND t.is_active = true
) table_cap ON true;