CREATE OR REPLACE VIEW vw_platform_images AS
SELECT
    pi.platform_images_id          AS platform_image_id,
    i.images_id           AS images_id,
    pi.tag         AS tag,
    i.storage_key   AS storage_key,
    i.original_name AS original_name,
    i.size_bytes    AS size_bytes,
    i.width        AS width,
    i.height       AS height,
    i.content_type  AS content_type,
    i.alt_text      AS alt_text,
    i.caption      AS caption,
    pi.is_primary   AS is_primary,
    pi.sort_order   AS sort_order,
    i.created_at    AS created_at
FROM platform_images pi
JOIN images i ON i.images_id = pi.images_id;
