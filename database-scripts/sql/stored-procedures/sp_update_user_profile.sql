CREATE OR REPLACE FUNCTION sp_update_user_profile(
    p_users_id uuid, p_first_name text, p_last_name text, p_phone text,
    p_address text, p_city text, p_state text, p_zip text, p_opt_in bool
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_address_id uuid;
BEGIN
    SELECT addresses_id INTO v_address_id FROM users WHERE users_id = p_users_id;
    IF v_address_id IS NULL AND (p_address IS NOT NULL OR p_city IS NOT NULL) THEN
        INSERT INTO addresses (line1, city, state, zip_code, created_at, updated_at)
        VALUES (COALESCE(p_address,''), COALESCE(p_city,''),
            COALESCE(p_state,''), COALESCE(p_zip,''), now(), now())
        RETURNING addresses_id INTO v_address_id;
        UPDATE users SET addresses_id = v_address_id WHERE users_id = p_users_id;
    ELSIF v_address_id IS NOT NULL THEN
        UPDATE addresses SET
            line1 = COALESCE(p_address, line1),
            city = COALESCE(p_city, city),
            state = COALESCE(p_state, state),
            zip_code = COALESCE(p_zip, zip_code),
            updated_at = now()
        WHERE addresses_id = v_address_id;
    END IF;
    UPDATE users SET
        first_name = COALESCE(p_first_name, first_name),
        last_name = COALESCE(p_last_name, last_name),
        phone = p_phone,
        opt_in_location_email = COALESCE(p_opt_in, opt_in_location_email),
        has_completed_onboarding = true,
        updated_at = now()
    WHERE users_id = p_users_id;
END; $$;
