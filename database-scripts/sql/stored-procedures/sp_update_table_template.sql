DROP FUNCTION IF EXISTS sp_update_table_template(uuid, text, int, text, text, int, bool);

CREATE OR REPLACE FUNCTION sp_update_table_template(
    p_id uuid, p_name text, p_capacity int, p_shape text,
    p_color text, p_price_cents int, p_is_active bool,
    p_default_row_span int DEFAULT NULL, p_default_col_span int DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE table_templates SET
        name = COALESCE(p_name, name),
        default_capacity = COALESCE(p_capacity, default_capacity),
        default_shape = COALESCE(p_shape, default_shape),
        default_color = p_color,
        default_price_cents = COALESCE(p_price_cents, default_price_cents),
        is_active = COALESCE(p_is_active, is_active),
        default_row_span = COALESCE(p_default_row_span, default_row_span),
        default_col_span = COALESCE(p_default_col_span, default_col_span),
        updated_at = now()
    WHERE table_templates_id = p_id;
END; $$;
