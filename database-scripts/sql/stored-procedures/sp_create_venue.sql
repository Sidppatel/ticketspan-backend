CREATE OR REPLACE FUNCTION sp_create_venue(
    p_tenants_id uuid, p_name text, p_description text, p_image_path text,
    p_phone text, p_email text, p_website text,
    p_line1 text, p_line2 text, p_city text, p_state text, p_zip text
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid; v_addr_id uuid;
BEGIN
    INSERT INTO addresses (line1, line2, city, state, zip_code, created_at, updated_at)
    VALUES (COALESCE(p_line1,''), p_line2, COALESCE(p_city,''),
        COALESCE(p_state,''), COALESCE(p_zip,''), now(), now())
    RETURNING addresses_id INTO v_addr_id;
    INSERT INTO venues (tenants_id, name, description, image_path, phone, email,
        website, is_active, addresses_id, created_at, updated_at)
    VALUES (p_tenants_id, p_name, p_description, p_image_path, p_phone, p_email,
        p_website, true, v_addr_id, now(), now())
    RETURNING venues_id INTO v_id;
    RETURN v_id;
END; $$;
