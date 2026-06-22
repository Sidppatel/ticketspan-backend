CREATE OR REPLACE FUNCTION sp_add_platform_image(
    p_storage_key text,
    p_original_name text,
    p_size_bytes int,
    p_width int,
    p_height int,
    p_uploaded_by uuid,
    p_tag text DEFAULT NULL,
    p_uploader_type text DEFAULT NULL,
    p_alt_text text DEFAULT NULL,
    p_caption text DEFAULT NULL,
    p_content_type text DEFAULT NULL,
    p_checksum text DEFAULT NULL
) RETURNS TABLE(
    images_id uuid,
    platform_image_id uuid,
    sort_order int,
    is_primary boolean
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_image_id uuid;
    v_platform_image_id uuid;
    v_sort_order int;
    v_is_primary boolean;
    v_has_primary boolean;
BEGIN
    SELECT COALESCE(MAX(pi.sort_order) + 1, 0) INTO v_sort_order
    FROM platform_images pi;

    SELECT EXISTS(SELECT 1 FROM platform_images pi WHERE pi.is_primary = true)
    INTO v_has_primary;
    v_is_primary := NOT v_has_primary;

    INSERT INTO images (images_id, entity_type, entity_id, storage_key, original_name,
        size_bytes, width, height, sort_order,
        uploaded_by_users_id, uploader_type, alt_text, caption, content_type, checksum,
        created_at, updated_at)
    VALUES (gen_random_uuid(), 'platform', gen_random_uuid(), p_storage_key, p_original_name,
        p_size_bytes, p_width, p_height, v_sort_order,
        p_uploaded_by, p_uploader_type, p_alt_text, p_caption, p_content_type, p_checksum,
        now(), now())
    RETURNING images_id INTO v_image_id;

    INSERT INTO platform_images (platform_images_id, images_id, tag, sort_order, is_primary,
        created_at, updated_at)
    VALUES (gen_random_uuid(), v_image_id, p_tag, v_sort_order, v_is_primary,
        now(), now())
    RETURNING platform_images_id INTO v_platform_image_id;

    RETURN QUERY SELECT v_image_id, v_platform_image_id, v_sort_order, v_is_primary;
END; $$;
