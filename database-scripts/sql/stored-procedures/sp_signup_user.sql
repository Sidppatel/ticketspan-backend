CREATE OR REPLACE FUNCTION sp_signup_user(
    p_tenants_id uuid,
    p_email text,
    p_email_hash text,
    p_first_name text,
    p_last_name text,
    p_password_hash text,
    p_pepper_version smallint DEFAULT 1,
    p_role smallint DEFAULT 0
) RETURNS SETOF users LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
BEGIN
    IF EXISTS (
        SELECT 1 FROM users
        WHERE email_hash = p_email_hash
          AND role = p_role
          AND tenants_id IS NOT DISTINCT FROM p_tenants_id
    ) THEN
        RAISE EXCEPTION 'User with this email already exists';
    END IF;

    INSERT INTO users (
        tenants_id, email, email_hash, first_name, last_name,
        password_hash, pepper_version, role,
        email_verified, is_active,
        opt_in_location_email, has_completed_onboarding,
        created_at, updated_at
    ) VALUES (
        p_tenants_id, p_email, p_email_hash, p_first_name, p_last_name,
        p_password_hash, p_pepper_version, p_role,
        false, true,
        false, false,
        now(), now()
    )
    RETURNING users_id INTO v_id;

    RETURN QUERY SELECT * FROM users WHERE users_id = v_id;
END;
$$;
