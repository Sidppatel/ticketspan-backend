CREATE OR REPLACE FUNCTION sp_get_primary_image_key(p_entity_type text, p_entity_id uuid)
RETURNS text LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT CASE
        WHEN p_entity_type = 'event' THEN (
            SELECT i.storage_key
            FROM event_images ei
            JOIN images i ON i.images_id = ei.images_id
            WHERE ei.events_id = p_entity_id AND ei.is_primary = true
            LIMIT 1
        )
        WHEN p_entity_type = 'venue' THEN (
            SELECT i.storage_key
            FROM venue_images vi
            JOIN images i ON i.images_id = vi.images_id
            WHERE vi.venues_id = p_entity_id AND vi.is_primary = true
            LIMIT 1
        )
        ELSE (
            SELECT storage_key FROM images
            WHERE entity_type = p_entity_type AND entity_id = p_entity_id
            ORDER BY sort_order ASC
            LIMIT 1
        )
    END;
$$;
