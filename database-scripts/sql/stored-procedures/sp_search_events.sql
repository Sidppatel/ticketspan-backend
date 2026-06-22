CREATE OR REPLACE FUNCTION sp_search_events(p_query text)
RETURNS TABLE(events_id uuid) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT events_id
      FROM events
     WHERE status = 'Published'
       AND (
           search_vector @@ plainto_tsquery('english', p_query)
           OR similarity(title, p_query) > 0.1
       );
$$;
