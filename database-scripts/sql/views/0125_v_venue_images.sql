CREATE OR REPLACE VIEW vw_venue_images AS
SELECT
    vi.venue_images_id          AS venue_image_id,
    vi.venues_id     AS venues_id,
    i.images_id           AS images_id,
    i.storage_key   AS storage_key,
    i.original_name AS original_name,
    i.size_bytes    AS size_bytes,
    i.width        AS width,
    i.height       AS height,
    i.content_type  AS content_type,
    i.alt_text      AS alt_text,
    i.caption      AS caption,
    vi.is_primary   AS is_primary,
    vi.sort_order   AS sort_order,
    i.created_at    AS created_at
FROM venue_images vi
JOIN images i ON i.images_id = vi.images_id;
