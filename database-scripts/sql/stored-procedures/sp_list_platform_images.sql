CREATE OR REPLACE FUNCTION sp_list_platform_images(p_tag text DEFAULT NULL)
RETURNS SETOF vw_platform_images LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM vw_platform_images
    WHERE p_tag IS NULL OR tag = p_tag
    ORDER BY sort_order ASC;
$$;
