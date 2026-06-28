DROP FUNCTION IF EXISTS sp_update_sponsor(uuid, text, text, text, jsonb);

CREATE OR REPLACE FUNCTION sp_update_sponsor(
    p_id uuid,
    p_name text,
    p_slug text,
    p_image_path text,
    p_meta jsonb,
    p_is_active bool DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE sponsors SET
        name = COALESCE(p_name, name),
        slug = COALESCE(p_slug, slug),
        primary_image_path = CASE
            WHEN p_image_path IS NULL THEN primary_image_path
            WHEN p_image_path = '' THEN NULL
            ELSE p_image_path
        END,
        meta = COALESCE(p_meta, meta),
        is_active = COALESCE(p_is_active, is_active),
        updated_at = now()
    WHERE sponsors_id = p_id;
END; $$;
