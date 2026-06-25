CREATE OR REPLACE FUNCTION sp_create_tenant(
    p_slug text,
    p_name text,
    p_admin_email text,
    p_admin_email_hash text,
    p_admin_first_name text,
    p_admin_last_name text,
    p_magic_token_hash text,
    p_magic_expires_at timestamptz,
    p_legal_name text DEFAULT NULL,
    p_country_code text DEFAULT 'US'
) RETURNS TABLE (out_tenants_id uuid, out_users_id uuid) LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_tenant uuid;
    v_user uuid;
BEGIN
    INSERT INTO tenants (
        slug, name, legal_name, country_code,
        stripe_charges_enabled, stripe_payouts_enabled, stripe_details_submitted,
        created_at, updated_at
    ) VALUES (
        p_slug, p_name, p_legal_name, COALESCE(p_country_code, 'US'),
        false, false, false,
        now(), now()
    )
    RETURNING tenants_id INTO v_tenant;

    INSERT INTO users (
        tenants_id, email, email_hash, first_name, last_name,
        role, is_active, email_verified,
        created_at, updated_at
    ) VALUES (
        v_tenant, p_admin_email, p_admin_email_hash, p_admin_first_name, p_admin_last_name,
        1, true, false,
        now(), now()
    )
    RETURNING users_id INTO v_user;

    -- Tenant admin setup is a first-time password set: store a password-reset
    -- token so the existing /set-password flow (sp_consume_password_reset_token)
    -- consumes it. The emailed link points to the admin portal /set-password page.
    INSERT INTO password_reset_tokens (
        users_id, token_hash, email, expires_at, is_used,
        created_at, updated_at
    ) VALUES (
        v_user, p_magic_token_hash, p_admin_email, p_magic_expires_at, false,
        now(), now()
    );

    RETURN QUERY SELECT v_tenant, v_user;
END; $$;
