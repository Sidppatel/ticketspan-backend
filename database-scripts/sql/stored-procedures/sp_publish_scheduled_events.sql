DROP FUNCTION IF EXISTS sp_publish_scheduled_events();

CREATE OR REPLACE FUNCTION sp_publish_scheduled_events() RETURNS SETOF uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    UPDATE events SET
        status = 'Published', published_at = now(),
        scheduled_publish_at = NULL, updated_at = now()
    WHERE status = 'Draft'
      AND scheduled_publish_at IS NOT NULL
      AND scheduled_publish_at <= now()
    RETURNING events_id;
END; $$;
