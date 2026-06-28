DROP FUNCTION IF EXISTS sp_update_table_template(uuid, text, int, text, text, int, bool);
DROP FUNCTION IF EXISTS sp_update_table_template(uuid, text, int, text, text, int, bool, int, int);
DROP FUNCTION IF EXISTS sp_update_table_template(uuid, text, int, text, text, int, bool, numeric, numeric);

-- Name is intentionally not updatable: it is locked after creation. p_name kept
-- in the signature for call-site compatibility but ignored.
CREATE OR REPLACE FUNCTION sp_update_table_template(
    p_id uuid, p_name text, p_capacity int, p_shape text,
    p_color text, p_price_cents int, p_is_active bool,
    p_default_width numeric DEFAULT NULL, p_default_height numeric DEFAULT NULL,
    p_is_all_inclusive bool DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE table_templates SET
        default_capacity = COALESCE(p_capacity, default_capacity),
        default_shape = COALESCE(p_shape, default_shape),
        default_color = p_color,
        default_price_cents = COALESCE(p_price_cents, default_price_cents),
        is_active = COALESCE(p_is_active, is_active),
        default_width = COALESCE(p_default_width, default_width),
        default_height = COALESCE(p_default_height, default_height),
        default_is_all_inclusive = COALESCE(p_is_all_inclusive, default_is_all_inclusive),
        updated_at = now()
    WHERE table_templates_id = p_id;
END; $$;
