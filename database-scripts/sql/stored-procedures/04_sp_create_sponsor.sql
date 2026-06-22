CREATE OR REPLACE FUNCTION sp_create_sponsor(
    p_tenants_id uuid,
    p_name text,
    p_slug text,
    p_image_path text,
    p_meta jsonb
) RETURNS uuid LANGUAGE plpgsql
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO sponsors (tenants_id, name, slug, primary_image_path, meta, created_at, updated_at)
    VALUES (
        p_tenants_id,
        p_name,
        p_slug,
        NULLIF(p_image_path, ''),
        COALESCE(p_meta, '[]'::jsonb),
        now(),
        now()
    )
    RETURNING sponsors_id INTO v_id;
    RETURN v_id;
END; $$;
