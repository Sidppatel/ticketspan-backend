CREATE OR REPLACE VIEW vw_event_ticket_types_summary AS
SELECT
    ett.event_ticket_types_id AS event_ticket_types_id, ett.events_id, ett.label, ett.price_cents,
    ett.platform_fee_cents, ett.max_quantity, ett.sort_order, ett.is_active,
    ett.description,
    ett.price_cents + COALESCE(ett.platform_fee_cents, 0) AS total_price_cents,
    COALESCE(bs.sold, 0)::int AS sold_count,
    CASE
        WHEN ett.max_quantity IS NULL THEN -1
        ELSE GREATEST(0, ett.max_quantity - COALESCE(bs.sold, 0))
    END::int AS available_count
FROM event_ticket_types ett
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(b.seats_reserved), 0)::int AS sold
    FROM purchases b
    WHERE b.event_ticket_types_id = ett.event_ticket_types_id
      AND b.status IN ('Pending', 'Paid', 'CheckedIn')
) bs ON true;