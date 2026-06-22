CREATE OR REPLACE FUNCTION sp_signin_user_google(
    p_tenants_id uuid,
    p_google_subject text,
    p_email text,
    p_email_hash text,
    p_first_name text,
    p_last_name text,
    p_role smallint DEFAULT 0
) RETURNS SETOF users LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
    v_existing_password_hash text;
    v_existing_google_subject text;
BEGIN
    SELECT users_id INTO v_id FROM users WHERE google_subject = p_google_subject;

    IF v_id IS NOT NULL THEN
        UPDATE users
        SET last_login_at = now(),
            updated_at = now()
        WHERE users_id = v_id;
        RETURN QUERY SELECT * FROM users WHERE users_id = v_id;
        RETURN;
    END IF;

    SELECT users_id, password_hash, google_subject
      INTO v_id, v_existing_password_hash, v_existing_google_subject
      FROM users
      WHERE email_hash = p_email_hash
        AND role = p_role
        AND tenants_id IS NOT DISTINCT FROM p_tenants_id;

    IF v_id IS NULL THEN
        INSERT INTO users (
            tenants_id, email, email_hash, first_name, last_name,
            password_hash, role, email_verified, email_verified_at,
            is_active, last_login_at,
            opt_in_location_email, has_completed_onboarding,
            google_subject, created_at, updated_at
        ) VALUES (
            p_tenants_id, p_email, p_email_hash, p_first_name, p_last_name,
            NULL, p_role, true, now(),
            true, now(),
            false, false,
            p_google_subject, now(), now()
        )
        RETURNING users_id INTO v_id;
        RETURN QUERY SELECT * FROM users WHERE users_id = v_id;
        RETURN;
    END IF;

    IF v_existing_google_subject IS NOT NULL AND v_existing_google_subject <> p_google_subject THEN
        RAISE EXCEPTION 'Google account already linked to a different identity'
            USING ERRCODE = 'P0001';
    END IF;

    IF v_existing_password_hash IS NOT NULL AND v_existing_google_subject IS NULL THEN
        RAISE EXCEPTION 'Existing password account requires password sign-in to link Google'
            USING ERRCODE = 'P0002';
    END IF;

    UPDATE users
    SET google_subject = p_google_subject,
        email_verified = true,
        email_verified_at = COALESCE(email_verified_at, now()),
        last_login_at = now(),
        updated_at = now()
    WHERE users_id = v_id;

    RETURN QUERY SELECT * FROM users WHERE users_id = v_id;
END;
$$;
