CREATE OR REPLACE FUNCTION sp_list_venue_images(p_venue_id uuid)
RETURNS SETOF vw_venue_images LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM vw_venue_images
    WHERE venues_id = p_venue_id
    ORDER BY sort_order ASC;
$$;
