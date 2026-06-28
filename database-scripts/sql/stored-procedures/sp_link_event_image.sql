DROP FUNCTION IF EXISTS sp_link_event_image(uuid, uuid, text);
CREATE OR REPLACE FUNCTION sp_link_event_image(
    p_event_id uuid,
    p_image_id uuid,
    p_type text DEFAULT 'event_image'
) RETURNS TABLE(
    event_image_id uuid,
    sort_order int,
    is_primary boolean,
    image_type text
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
    SELECT e.tenants_id INTO v_tenant FROM events e WHERE e.events_id = p_event_id;

    SELECT count(*) INTO v_count
    FROM event_images ei WHERE ei.events_id = p_event_id AND ei.type = p_type;
    IF v_count >= 5 THEN
        RAISE EXCEPTION 'image limit reached for type %', p_type;
    END IF;

    SELECT COALESCE(MAX(ei.sort_order) + 1, 0) INTO v_sort
    FROM event_images ei WHERE ei.events_id = p_event_id AND ei.type = p_type;

    SELECT NOT EXISTS(
        SELECT 1 FROM event_images ei
        WHERE ei.events_id = p_event_id AND ei.type = p_type AND ei.is_primary = true
    ) INTO v_is_primary;

    RETURN QUERY
    WITH inserted AS (
        INSERT INTO event_images (tenants_id, events_id, images_id, sort_order, is_primary, type,
            created_at, updated_at)
        VALUES (v_tenant, p_event_id, p_image_id, v_sort, v_is_primary, p_type, now(), now())
        RETURNING event_images_id, sort_order, is_primary, type
    )
    SELECT inserted.event_images_id, inserted.sort_order, inserted.is_primary, inserted.type::text FROM inserted;
END; $$;
