CREATE OR REPLACE FUNCTION sp_list_venue_images(p_venue_id uuid)
RETURNS TABLE(
    venue_image_id uuid,
    venues_id uuid,
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
    created_at timestamptz
) LANGUAGE plpgsql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    SELECT vi.venue_image_id, vi.venues_id, vi.images_id, vi.storage_key, vi.original_name,
           vi.size_bytes, vi.width, vi.height, vi.content_type, vi.alt_text,
           vi.caption, vi.is_primary, vi.sort_order, vi.created_at
    FROM vw_venue_images vi
    WHERE vi.venues_id = p_venue_id
    ORDER BY vi.sort_order ASC;
END;
$$;
