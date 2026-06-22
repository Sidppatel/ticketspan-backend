CREATE OR REPLACE FUNCTION sp_update_tenant(
    p_tenants_id uuid,
    p_name text DEFAULT NULL,
    p_legal_name text DEFAULT NULL,
    p_country_code text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tenants SET
        name         = COALESCE(p_name, name),
        legal_name   = COALESCE(p_legal_name, legal_name),
        country_code = COALESCE(p_country_code, country_code),
        updated_at   = now()
    WHERE tenants_id = p_tenants_id
      AND archived_at IS NULL;
END; $$;
