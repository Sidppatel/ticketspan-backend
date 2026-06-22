CREATE OR REPLACE VIEW vw_event_tables_summary AS
SELECT
    et.event_tables_id AS event_tables_id, et.events_id, et.label, et.capacity,
    et.shape::text, et.color, et.price_cents, et.platform_fee_cents,
    COALESCE(et.row_span, 1)::int AS default_row_span,
    COALESCE(et.col_span, 1)::int AS default_col_span,
    et.is_active,
    COALESCE(ts.total, 0)::int AS total_tables,
    COALESCE(ts.available, 0)::int AS available_tables,
    COALESCE(ts.locked, 0)::int AS locked_tables,
    COALESCE(ts.booked, 0)::int AS booked_tables
FROM event_tables et
LEFT JOIN LATERAL (
    SELECT
        COUNT(*)::int AS total,
        COUNT(*) FILTER (WHERE t.status = 'Available' AND t.is_active)::int AS available,
        COUNT(*) FILTER (WHERE t.status = 'Locked')::int AS locked,
        COUNT(*) FILTER (WHERE t.status = 'Booked')::int AS booked
    FROM tables t WHERE t.event_tables_id = et.event_tables_id
) ts ON true;