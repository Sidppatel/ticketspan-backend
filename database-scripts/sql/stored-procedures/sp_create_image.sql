CREATE OR REPLACE FUNCTION sp_create_image(
    p_entity_type text, p_entity_id uuid, p_storage_key text, p_original_name text,
    p_size_bytes int, p_width int, p_height int,
    p_sort_order int, p_uploaded_by uuid,
    p_uploader_type text DEFAULT NULL,
    p_alt_text text DEFAULT NULL,
    p_caption text DEFAULT NULL,
    p_content_type text DEFAULT NULL,
    p_checksum text DEFAULT NULL,
    p_tenants_id uuid DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO images (tenants_id, entity_type, entity_id, storage_key, original_name,
        size_bytes, width, height, sort_order, uploaded_by_users_id, uploader_type,
        alt_text, caption, content_type, checksum,
        created_at, updated_at)
    VALUES (p_tenants_id, p_entity_type, p_entity_id, p_storage_key, p_original_name,
        p_size_bytes, p_width, p_height, p_sort_order, p_uploaded_by, p_uploader_type,
        p_alt_text, p_caption, p_content_type, p_checksum,
        now(), now())
    RETURNING images_id INTO v_id;
    RETURN v_id;
END; $$;
