DROP FUNCTION IF EXISTS sp_update_tenant_branding(uuid, uuid, text, text, text);

CREATE OR REPLACE FUNCTION sp_update_tenant_branding(
    p_tenants_id uuid,
    p_logo_images_id uuid DEFAULT NULL,
    p_brand_primary text DEFAULT NULL,
    p_brand_secondary text DEFAULT NULL,
    p_brand_accent text DEFAULT NULL,
    p_brand_background text DEFAULT NULL,
    p_brand_text text DEFAULT NULL,
    p_brand_button text DEFAULT NULL,
    p_brand_highlight text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tenants SET
        logo_images_id = p_logo_images_id,
        brand_primary  = p_brand_primary,
        brand_secondary = p_brand_secondary,
        brand_accent   = p_brand_accent,
        brand_background = p_brand_background,
        brand_text     = p_brand_text,
        brand_button   = p_brand_button,
        brand_highlight = p_brand_highlight,
        updated_at     = now()
    WHERE tenants_id = p_tenants_id
      AND archived_at IS NULL;
END; $$;
