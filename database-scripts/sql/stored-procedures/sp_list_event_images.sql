DROP FUNCTION IF EXISTS sp_list_event_images(uuid);
DROP FUNCTION IF EXISTS sp_list_event_images(uuid, text);
CREATE OR REPLACE FUNCTION sp_list_event_images(p_event_id uuid, p_type text DEFAULT NULL)
RETURNS TABLE(
    event_image_id uuid,
    events_id uuid,
    images_id uuid,
    storage_key text,
    original_name text,
    size_bytes bigint,
    width int,
    height int,
    content_type text,
    alt_text text,
    caption text,
    is_primary boolean,
    sort_order int,
    created_at timestamptz,
    type text
) LANGUAGE plpgsql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    SELECT ei.event_image_id, ei.events_id, ei.images_id, ei.storage_key, ei.original_name,
           ei.size_bytes, ei.width, ei.height, ei.content_type, ei.alt_text,
           ei.caption, ei.is_primary, ei.sort_order, ei.created_at, ei.type
    FROM vw_event_images ei
    WHERE ei.events_id = p_event_id
      AND (p_type IS NULL OR ei.type = p_type)
    ORDER BY ei.is_primary DESC, ei.sort_order ASC;
END;
$$;
