DROP FUNCTION IF EXISTS sp_signin_user_google(uuid, text, text, text, text, text, smallint);

CREATE OR REPLACE FUNCTION sp_signin_user_google(
    p_tenants_id uuid,
    p_google_subject text,
    p_email text,
    p_email_hash text,
    p_first_name text,
    p_last_name text,
    p_role smallint DEFAULT 0,
    p_allowed_roles smallint[] DEFAULT ARRAY[0]::smallint[]
) RETURNS SETOF users LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
    v_is_active boolean;
    v_existing_google_subject text;
BEGIN
    SELECT users_id, is_active INTO v_id, v_is_active
      FROM users
      WHERE google_subject = p_google_subject
        AND role = ANY(p_allowed_roles)
        AND (p_tenants_id IS NULL OR tenants_id IS NOT DISTINCT FROM p_tenants_id OR role = 99)
      ORDER BY role DESC
      LIMIT 1;

    IF v_id IS NOT NULL THEN
        IF NOT v_is_active THEN
            RAISE EXCEPTION 'Account disabled' USING ERRCODE = 'P0003';
        END IF;
        UPDATE users
        SET last_login_at = now(),
            updated_at = now()
        WHERE users_id = v_id;
        RETURN QUERY SELECT * FROM users WHERE users_id = v_id;
        RETURN;
    END IF;

    IF NOT (p_role = ANY(p_allowed_roles)) THEN
        RAISE EXCEPTION 'Google sign-in is not linked to this account'
            USING ERRCODE = 'P0004';
    END IF;

    SELECT users_id, is_active, google_subject
      INTO v_id, v_is_active, v_existing_google_subject
      FROM users
      WHERE email_hash = p_email_hash
        AND role = ANY(p_allowed_roles)
        AND (p_tenants_id IS NULL OR tenants_id IS NOT DISTINCT FROM p_tenants_id OR role = 99)
      ORDER BY role DESC
      LIMIT 1;

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

    IF NOT v_is_active THEN
        RAISE EXCEPTION 'Account disabled' USING ERRCODE = 'P0003';
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
