CREATE OR REPLACE FUNCTION sp_list_event_images(p_event_id uuid)
RETURNS SETOF vw_event_images LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM vw_event_images
    WHERE events_id = p_event_id
    ORDER BY sort_order ASC;
$$;
