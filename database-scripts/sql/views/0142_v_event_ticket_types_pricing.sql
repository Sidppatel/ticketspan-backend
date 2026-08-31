CREATE OR REPLACE VIEW vw_event_ticket_types_pricing AS
SELECT
    tt.events_id,
    tt.is_active,
    tt.sort_order,
    tt.event_ticket_types_id,
    tt.label,
    tt.price_cents,
    COALESCE(tt.platform_fee_cents, 0) AS platform_fee_cents,
    COALESCE(tt.max_quantity, 0) AS max_quantity,
    COALESCE(tt.description, '') AS description,
    tt.fee_formulas_id,
    COALESCE(tt.capacity, 0) AS capacity,
    COALESCE(bp.selling_price_cents, tt.price_cents) AS selling_price_cents,
    COALESCE(vs.sold_count, 0) AS sold_count,
    COALESCE(bp.platform_fee_cents + bp.gateway_fee_cents, 0) AS service_fee_cents,
    COALESCE(bp.tax_cents, 0) AS tax_cents,
    COALESCE(bp.final_price_cents, tt.price_cents) AS total_cents,
    COALESCE(vs.available_count, CASE WHEN tt.max_quantity > 0 THEN tt.max_quantity - COALESCE(vs.sold_count, 0) WHEN tt.capacity > 0 THEN tt.capacity - COALESCE(vs.sold_count, 0) ELSE -1 END) AS available_quantity,
    CASE WHEN (COALESCE(vs.available_count, CASE WHEN tt.max_quantity > 0 THEN tt.max_quantity - COALESCE(vs.sold_count, 0) WHEN tt.capacity > 0 THEN tt.capacity - COALESCE(vs.sold_count, 0) ELSE -1 END)) = 0 THEN true ELSE false END AS is_sold_out
FROM event_ticket_types tt
LEFT JOIN vw_event_ticket_types_summary vs ON vs.event_ticket_types_id = tt.event_ticket_types_id
LEFT JOIN prices p ON p.events_id = tt.events_id AND p.pricing_type = 'TicketTier'
    AND lower(p.name) = lower(tt.label) AND p.is_active
LEFT JOIN LATERAL app.price_breakdown(p.prices_id, now(), 1, -1) bp ON p.prices_id IS NOT NULL;
