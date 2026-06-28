DROP FUNCTION IF EXISTS sp_update_venue(uuid, text, text, text, text, text, text, bool, text, text, text, text);

CREATE OR REPLACE FUNCTION sp_update_venue(
    p_id uuid, p_name text, p_description text, p_image_path text,
    p_phone text, p_email text, p_website text, p_is_active bool,
    p_line1 text, p_line2 text, p_city text, p_state text, p_zip text
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_addr_id uuid;
BEGIN
    SELECT addresses_id INTO v_addr_id FROM venues WHERE venues_id = p_id;
    IF v_addr_id IS NOT NULL THEN
        UPDATE addresses SET
            line1 = COALESCE(p_line1, line1),
            line2 = COALESCE(p_line2, line2),
            city = COALESCE(p_city, city),
            state = COALESCE(p_state, state),
            zip_code = COALESCE(p_zip, zip_code),
            updated_at = now()
        WHERE addresses_id = v_addr_id;
    END IF;
    UPDATE venues SET
        name = COALESCE(p_name, name),
        description = COALESCE(p_description, description),
        image_path = COALESCE(p_image_path, image_path),
        phone = p_phone, email = p_email, website = p_website,
        is_active = COALESCE(p_is_active, is_active),
        updated_at = now()
    WHERE venues_id = p_id;
END; $$;
