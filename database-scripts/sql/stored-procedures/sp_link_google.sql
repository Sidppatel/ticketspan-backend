CREATE OR REPLACE FUNCTION sp_link_google(
    p_users_id uuid,
    p_google_subject text
) RETURNS SETOF users LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_other uuid;
BEGIN
    SELECT o.users_id INTO v_other
      FROM users o
      JOIN users me ON me.users_id = p_users_id
      WHERE o.google_subject = p_google_subject
        AND o.users_id <> p_users_id
        AND o.role = me.role
        AND o.tenants_id IS NOT DISTINCT FROM me.tenants_id;

    IF v_other IS NOT NULL THEN
        RAISE EXCEPTION 'Google account already linked to a different identity'
            USING ERRCODE = 'P0001';
    END IF;

    UPDATE users
    SET google_subject = p_google_subject,
        email_verified = true,
        email_verified_at = COALESCE(email_verified_at, now()),
        updated_at = now()
    WHERE users_id = p_users_id;

    RETURN QUERY SELECT * FROM users WHERE users_id = p_users_id;
END; $$;
