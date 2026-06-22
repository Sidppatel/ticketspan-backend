CREATE OR REPLACE VIEW vw_event_facets AS
SELECT
    e.events_id                       AS events_id,
    e.status::text             AS status,
    e.end_date                  AS end_date,
    COALESCE(e.category::text, '') AS category,
    v.venues_id                       AS venues_id,
    v.name::text               AS venue_name,
    COALESCE(addr.city, '')::text AS venue_city,
    ettp.min_price               AS price_per_person_cents
FROM events e
JOIN venues v ON v.venues_id = e.venues_id
LEFT JOIN addresses addr ON v.addresses_id = addr.addresses_id
LEFT JOIN LATERAL (
    SELECT MIN(ett.price_cents)::int AS min_price
    FROM event_ticket_types ett
    WHERE ett.events_id = e.events_id AND ett.is_active = true
) ettp ON true;
