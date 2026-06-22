CREATE OR REPLACE VIEW vw_event_images AS
SELECT
    ei.event_images_id          AS event_image_id,
    ei.events_id     AS events_id,
    i.images_id           AS images_id,
    i.storage_key   AS storage_key,
    i.original_name AS original_name,
    i.size_bytes    AS size_bytes,
    i.width        AS width,
    i.height       AS height,
    i.content_type  AS content_type,
    i.alt_text      AS alt_text,
    i.caption      AS caption,
    ei.is_primary   AS is_primary,
    ei.sort_order   AS sort_order,
    i.created_at    AS created_at
FROM event_images ei
JOIN images i ON i.images_id = ei.images_id;
