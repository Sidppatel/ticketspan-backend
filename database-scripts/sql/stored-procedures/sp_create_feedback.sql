CREATE OR REPLACE FUNCTION sp_create_feedback(
    p_name text, p_email text, p_type text, p_message text, p_rating int,
    p_user_id uuid, p_user_agent text, p_ip text, p_diagnostics jsonb,
    p_tenants_id uuid DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO feedbacks (tenants_id, name, email, type, message, rating,
        users_id, user_agent, ip_address, diagnostics, created_at, updated_at)
    VALUES (p_tenants_id, p_name, p_email, p_type, p_message, p_rating,
        p_user_id, p_user_agent, p_ip, p_diagnostics, now(), now())
    RETURNING feedbacks_id INTO v_id;
    RETURN v_id;
END; $$;
