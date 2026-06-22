CREATE OR REPLACE FUNCTION sp_upsert_user(
    p_tenants_id uuid, p_email text, p_email_hash text,
    p_first_name text, p_last_name text, p_role smallint DEFAULT 0
) RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    SELECT users_id INTO v_id FROM users
    WHERE email = p_email
      AND role = p_role
      AND tenants_id IS NOT DISTINCT FROM p_tenants_id;
    IF v_id IS NULL THEN
        INSERT INTO users (tenants_id, email, email_hash, first_name, last_name, role,
            is_active, email_verified, last_login_at, opt_in_location_email, has_completed_onboarding,
            created_at, updated_at)
        VALUES (p_tenants_id, p_email, p_email_hash, p_first_name, p_last_name, p_role,
            true, true, now(), false, false, now(), now())
        RETURNING users_id INTO v_id;
    ELSE
        UPDATE users SET last_login_at = now(), email_verified = true, updated_at = now()
        WHERE users_id = v_id;
    END IF;
    RETURN v_id;
END; $$;
