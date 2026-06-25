DROP FUNCTION IF EXISTS sp_create_event_table(uuid, text, int, text, text, int, int, uuid, int, int);

CREATE OR REPLACE FUNCTION sp_create_event_table(
    p_event_id uuid, p_label text, p_capacity int, p_shape text, p_color text,
    p_price_cents int, p_fee_formulas_id uuid, p_template_id uuid,
    p_row_span int DEFAULT NULL, p_col_span int DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO event_tables (tenants_id, events_id, label, capacity, shape, color,
        price_cents, fee_formulas_id, platform_fee_cents, is_active, table_templates_id,
        row_span, col_span, created_at, updated_at)
    VALUES ((SELECT tenants_id FROM events WHERE events_id = p_event_id),
        p_event_id, p_label, p_capacity, p_shape, p_color,
        p_price_cents, p_fee_formulas_id, app.compute_fee(p_price_cents, p_fee_formulas_id),
        true, p_template_id,
        p_row_span, p_col_span, now(), now())
    RETURNING event_tables_id INTO v_id;
    RETURN v_id;
END; $$;
