CREATE OR REPLACE VIEW vw_tables AS
SELECT
    t.tables_id AS tables_id, t.events_id, t.event_tables_id,
    t.label, t.pos_x, t.pos_y,
    t.width, t.height,
    t.is_active, t.sort_order,
    t.status::text,
    t.locked_by_users_id, t.lock_expires_at,
    t.created_at, t.updated_at,
    et.capacity, et.shape::text, et.color,
    et.price_cents, et.platform_fee_cents,
    et.price_cents + COALESCE(et.platform_fee_cents, 0) AS total_price_cents,
    et.label AS event_table_label
FROM tables t
JOIN event_tables et ON t.event_tables_id = et.event_tables_id;