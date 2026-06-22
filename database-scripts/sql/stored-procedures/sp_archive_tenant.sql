CREATE OR REPLACE FUNCTION sp_archive_tenant(
    p_tenants_id uuid
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_archived_at timestamptz;
BEGIN
    SELECT archived_at INTO v_archived_at
    FROM tenants WHERE tenants_id = p_tenants_id FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Tenant % not found', p_tenants_id USING ERRCODE = 'no_data_found';
    END IF;

    IF v_archived_at IS NOT NULL THEN
        RETURN;
    END IF;

    UPDATE users SET is_active = false, updated_at = now()
    WHERE tenants_id = p_tenants_id;

    UPDATE tenants SET archived_at = now(), updated_at = now()
    WHERE tenants_id = p_tenants_id;
END; $$;
