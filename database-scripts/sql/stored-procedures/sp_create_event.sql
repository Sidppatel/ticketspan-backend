DROP FUNCTION IF EXISTS sp_create_event CASCADE;

CREATE OR REPLACE FUNCTION sp_create_event(
    p_tenants_id uuid, p_title text, p_slug text, p_description text, p_status text, p_category text,
    p_start_date timestamptz, p_end_date timestamptz, p_image_path text, p_is_featured bool,
    p_layout_mode text, p_price_per_person_cents int,
    p_platform_fee_percent int, p_platform_fee_cents int,
    p_venue_id uuid, p_created_by_users_id uuid,
    p_scheduled_publish_at timestamptz DEFAULT NULL, p_event_type text DEFAULT NULL,
    p_short_description text DEFAULT NULL,
    p_story_description text DEFAULT NULL,
    p_hero_backdrop_image_id uuid DEFAULT NULL,
    p_poster_image_id uuid DEFAULT NULL,
    p_is_verified_organizer bool DEFAULT TRUE,
    p_urgency_badge_text text DEFAULT NULL,
    p_tax_exempt bool DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
    v_slug text;
    v_event_type text;
    v_tax_exempt bool;
BEGIN
    PERFORM app.assert_tenant_sellable(p_tenants_id);

    v_event_type := NULLIF(p_event_type, '');
    IF v_event_type IS NULL THEN
        v_event_type := CASE WHEN p_layout_mode = 'Grid' THEN 'Table' ELSE 'Open' END;
    END IF;

    v_slug := NULLIF(trim(p_slug), '');
    IF v_slug IS NULL THEN
        v_slug := trim(both '-' from lower(regexp_replace(p_title, '[^a-zA-Z0-9]+', '-', 'g')));
        IF v_slug = '' THEN
            v_slug := 'event';
        END IF;
        v_slug := v_slug || '-' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 6);
    END IF;

    IF p_tax_exempt IS NULL THEN
        SELECT NOT COALESCE(charge_tax_by_default, true) INTO v_tax_exempt
          FROM tenants WHERE tenants_id = p_tenants_id;
    ELSE
        v_tax_exempt := p_tax_exempt;
    END IF;

    INSERT INTO events (tenants_id, title, slug, description, status, category,
        start_date, end_date, image_path, is_featured, layout_mode, event_type,
        venues_id, created_by_users_id,
        scheduled_publish_at, published_at, created_at, updated_at,
        short_description, story_description, hero_backdrop_image_id, poster_image_id, is_verified_organizer, urgency_badge_text,
        tax_exempt)
    VALUES (p_tenants_id, p_title, v_slug, p_description, p_status,
        CASE WHEN p_category = '' THEN NULL ELSE p_category END,
        p_start_date, p_end_date, p_image_path, COALESCE(p_is_featured, false), p_layout_mode,
        v_event_type,
        p_venue_id, p_created_by_users_id,
        p_scheduled_publish_at,
        CASE WHEN p_status = 'Published' THEN now() ELSE NULL END,
        now(), now(),
        p_short_description, p_story_description, p_hero_backdrop_image_id, p_poster_image_id, COALESCE(p_is_verified_organizer, true), p_urgency_badge_text,
        COALESCE(v_tax_exempt, false))
    RETURNING events_id INTO v_id;
    RETURN v_id;
END; $$;
