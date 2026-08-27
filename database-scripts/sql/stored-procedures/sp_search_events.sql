DROP FUNCTION IF EXISTS sp_search_events(text);
DROP FUNCTION IF EXISTS sp_search_events(text, uuid);
CREATE OR REPLACE FUNCTION sp_search_events(p_query text, p_tenant_id uuid DEFAULT NULL)
RETURNS TABLE(events_id uuid, title text, slug text, status text) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT events_id, title, slug, status::text
      FROM events
     WHERE status = 'Published'
       AND (p_tenant_id IS NULL OR tenants_id = p_tenant_id)
       AND (
           search_vector @@ plainto_tsquery('english', p_query)
           OR similarity(title, p_query) > 0.1
       );
$$;
