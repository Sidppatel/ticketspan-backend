CREATE OR REPLACE FUNCTION sp_update_event(
    p_id uuid, p_title text, p_slug text, p_description text, p_category text,
    p_start_date timestamptz, p_end_date timestamptz, p_image_path text, p_is_featured bool,
    p_layout_mode text, p_max_capacity int, p_price_per_person_cents int,
    p_platform_fee_percent int, p_platform_fee_cents int,
    p_grid_rows int, p_grid_cols int, p_venue_id uuid,
    p_scheduled_publish_at timestamptz DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE events SET
        title = COALESCE(p_title, title),
        slug = COALESCE(p_slug, slug),
        description = COALESCE(p_description, description),
        category = CASE WHEN p_category IS NULL THEN category
                           WHEN p_category = '' THEN NULL
                           ELSE p_category END,
        start_date = COALESCE(p_start_date, start_date),
        end_date = COALESCE(p_end_date, end_date),
        image_path = COALESCE(p_image_path, image_path),
        is_featured = COALESCE(p_is_featured, is_featured),
        layout_mode = COALESCE(p_layout_mode, layout_mode),
        max_capacity = p_max_capacity,
        grid_rows = p_grid_rows,
        grid_cols = p_grid_cols,
        venues_id = COALESCE(p_venue_id, venues_id),
        scheduled_publish_at = p_scheduled_publish_at,
        updated_at = now()
    WHERE events_id = p_id;
END; $$;