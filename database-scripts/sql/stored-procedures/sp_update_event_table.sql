DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool);
DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool, int);

CREATE OR REPLACE FUNCTION sp_update_event_table(
    p_id uuid, p_label text, p_capacity int, p_shape text, p_color text,
    p_price_cents int, p_is_active bool, p_platform_fee_cents int,
    p_row_span int DEFAULT NULL, p_col_span int DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE event_tables SET
        label = COALESCE(p_label, label),
        capacity = COALESCE(p_capacity, capacity),
        shape = COALESCE(p_shape, shape),
        color = CASE WHEN p_color IS NOT NULL THEN p_color ELSE color END,
        price_cents = COALESCE(p_price_cents, price_cents),
        is_active = COALESCE(p_is_active, is_active),
        platform_fee_cents = COALESCE(p_platform_fee_cents, platform_fee_cents),
        row_span = COALESCE(p_row_span, row_span),
        col_span = COALESCE(p_col_span, col_span),
        updated_at = now()
    WHERE event_tables_id = p_id;
END; $$;
