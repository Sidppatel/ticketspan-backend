DROP FUNCTION IF EXISTS sp_update_event CASCADE;

CREATE OR REPLACE FUNCTION sp_update_event(
    p_id uuid, p_title text, p_slug text, p_description text, p_category text,
    p_start_date timestamptz, p_end_date timestamptz, p_image_path text, p_is_featured bool,
    p_layout_mode text, p_price_per_person_cents int,
    p_platform_fee_percent int, p_platform_fee_cents int,
    p_venue_id uuid,
    p_scheduled_publish_at timestamptz DEFAULT NULL, p_event_type text DEFAULT NULL,
    p_meta jsonb DEFAULT NULL,
    p_short_description text DEFAULT NULL,
    p_story_description text DEFAULT NULL,
    p_hero_backdrop_image_id uuid DEFAULT NULL,
    p_poster_image_id uuid DEFAULT NULL,
    p_is_verified_organizer bool DEFAULT NULL,
    p_urgency_badge_text text DEFAULT NULL,
    p_tax_exempt bool DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE events SET
        title = COALESCE(p_title, title),
        slug = COALESCE(p_slug, slug),
        description = COALESCE(p_description, description),
        category = CASE WHEN p_category IS NULL THEN category WHEN p_category = '' THEN NULL ELSE p_category END,
        start_date = COALESCE(p_start_date, start_date),
        end_date = COALESCE(p_end_date, end_date),
        image_path = COALESCE(p_image_path, image_path),
        is_featured = COALESCE(p_is_featured, is_featured),
        layout_mode = COALESCE(p_layout_mode, layout_mode),
        event_type = COALESCE(NULLIF(p_event_type, ''), event_type),
        venues_id = COALESCE(p_venue_id, venues_id),
        scheduled_publish_at = p_scheduled_publish_at,
        meta = COALESCE(p_meta, meta),
        short_description = p_short_description,
        story_description = p_story_description,
        hero_backdrop_image_id = p_hero_backdrop_image_id,
        poster_image_id = p_poster_image_id,
        is_verified_organizer = COALESCE(p_is_verified_organizer, is_verified_organizer),
        urgency_badge_text = p_urgency_badge_text,
        tax_exempt = COALESCE(p_tax_exempt, tax_exempt),
        updated_at = now()
    WHERE events_id = p_id;
END; $$;
