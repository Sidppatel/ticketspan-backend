CREATE OR REPLACE FUNCTION sp_get_public_tenant_branding(
    p_slug text
) RETURNS TABLE (
    slug text,
    name text,
    logo_images_id uuid,
    brand_primary text,
    brand_secondary text,
    brand_accent text,
    brand_background text,
    brand_text text,
    brand_button text,
    brand_highlight text
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    SELECT t.slug::text, t.name::text, t.logo_images_id,
           t.brand_primary, t.brand_secondary, t.brand_accent,
           t.brand_background, t.brand_text, t.brand_button, t.brand_highlight
    FROM tenants t
    WHERE t.slug = p_slug
      AND t.archived_at IS NULL;
END; $$;
