CREATE OR REPLACE FUNCTION sp_signup_attendee(
    p_tenants_id uuid,
    p_email text,
    p_email_hash text,
    p_first_name text,
    p_last_name text,
    p_password_hash text
) RETURNS SETOF users LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
BEGIN
    INSERT INTO users (
        tenants_id, email, email_hash, first_name, last_name,
        password_hash, role, email_verified, email_verified_at,
        is_active, last_login_at,
        opt_in_location_email, has_completed_onboarding,
        created_at, updated_at
    ) VALUES (
        p_tenants_id, p_email, p_email_hash, p_first_name, p_last_name,
        p_password_hash, 0, false, NULL,
        true, now(),
        false, false,
        now(), now()
    )
    RETURNING users_id INTO v_id;

    RETURN QUERY SELECT * FROM users WHERE users_id = v_id;
END;
$$;
