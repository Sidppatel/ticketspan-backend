CREATE OR REPLACE FUNCTION sp_create_event(
    p_tenants_id uuid, p_title text, p_slug text, p_description text, p_status text, p_category text,
    p_start_date timestamptz, p_end_date timestamptz, p_image_path text, p_is_featured bool,
    p_layout_mode text, p_max_capacity int, p_price_per_person_cents int,
    p_platform_fee_percent int, p_platform_fee_cents int,
    p_grid_rows int, p_grid_cols int, p_venue_id uuid, p_created_by_users_id uuid,
    p_scheduled_publish_at timestamptz DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
    v_slug text;
BEGIN
    v_slug := NULLIF(trim(p_slug), '');
    IF v_slug IS NULL THEN
        v_slug := trim(both '-' from lower(regexp_replace(p_title, '[^a-zA-Z0-9]+', '-', 'g')));
        IF v_slug = '' THEN
            v_slug := 'event';
        END IF;
        v_slug := v_slug || '-' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 6);
    END IF;

    INSERT INTO events (tenants_id, title, slug, description, status, category,
        start_date, end_date, image_path, is_featured, layout_mode,
        max_capacity, grid_rows, grid_cols, venues_id, created_by_users_id,
        scheduled_publish_at, published_at, created_at, updated_at)
    VALUES (p_tenants_id, p_title, v_slug, p_description, p_status,
        CASE WHEN p_category = '' THEN NULL ELSE p_category END,
        p_start_date, p_end_date, p_image_path, COALESCE(p_is_featured, false), p_layout_mode,
        p_max_capacity, p_grid_rows, p_grid_cols, p_venue_id, p_created_by_users_id,
        p_scheduled_publish_at,
        CASE WHEN p_status = 'Published' THEN now() ELSE NULL END,
        now(), now())
    RETURNING events_id INTO v_id;
    RETURN v_id;
END; $$;
