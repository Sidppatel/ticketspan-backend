CREATE OR REPLACE FUNCTION sp_create_platform_lead(
    p_name text, p_company_name text, p_phone text, p_website text, p_description text
) RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO platform_leads (name, company_name, phone, website, description, created_at, updated_at)
    VALUES (p_name, p_company_name, p_phone, p_website, p_description, now(), now())
    RETURNING platform_leads_id INTO v_id;
    RETURN v_id;
END; $$;
