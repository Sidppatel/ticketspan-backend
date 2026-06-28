DROP FUNCTION IF EXISTS sp_create_table_template(uuid, text, int, text, text, int, int, int);
DROP FUNCTION IF EXISTS sp_create_table_template(uuid, text, int, text, text, int, numeric, numeric);

CREATE OR REPLACE FUNCTION sp_create_table_template(
    p_tenants_id uuid, p_name text, p_capacity int, p_shape text, p_color text, p_price_cents int,
    p_default_width numeric DEFAULT 80, p_default_height numeric DEFAULT 80,
    p_is_all_inclusive bool DEFAULT true
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO table_templates (tenants_id, name, default_capacity, default_shape,
        default_color, default_price_cents, default_width, default_height,
        default_is_all_inclusive, is_active, created_at, updated_at)
    VALUES (p_tenants_id, p_name, p_capacity, p_shape,
        p_color, p_price_cents,
        COALESCE(p_default_width, 80), COALESCE(p_default_height, 80),
        COALESCE(p_is_all_inclusive, true), true, now(), now())
    RETURNING table_templates_id INTO v_id;
    RETURN v_id;
END; $$;
