DROP FUNCTION IF EXISTS sp_link_venue_image(uuid, uuid);
CREATE OR REPLACE FUNCTION sp_link_venue_image(
    p_venue_id uuid,
    p_image_id uuid
) RETURNS TABLE(
    venue_image_id uuid,
    sort_order int,
    is_primary boolean,
    storage_key text
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
#variable_conflict use_column
DECLARE
    v_tenant uuid;
    v_sort int;
    v_is_primary boolean;
    v_count int;
BEGIN
    SELECT v.tenants_id INTO v_tenant FROM venues v WHERE v.venues_id = p_venue_id;

    SELECT count(*) INTO v_count
    FROM venue_images vi WHERE vi.venues_id = p_venue_id;
    IF v_count >= 10 THEN
        RAISE EXCEPTION 'image limit reached for venue';
    END IF;

    SELECT COALESCE(MAX(vi.sort_order) + 1, 0) INTO v_sort
    FROM venue_images vi WHERE vi.venues_id = p_venue_id;

    SELECT NOT EXISTS(
        SELECT 1 FROM venue_images vi
        WHERE vi.venues_id = p_venue_id AND vi.is_primary = true
    ) INTO v_is_primary;

    RETURN QUERY
    WITH inserted AS (
        INSERT INTO venue_images (tenants_id, venues_id, images_id, sort_order, is_primary,
            created_at, updated_at)
        VALUES (v_tenant, p_venue_id, p_image_id, v_sort, v_is_primary, now(), now())
        RETURNING venue_images_id, sort_order, is_primary, images_id
    )
    SELECT inserted.venue_images_id, inserted.sort_order, inserted.is_primary, img.storage_key
    FROM inserted
    JOIN images img ON img.images_id = inserted.images_id;
END; $$;
