CREATE OR REPLACE FUNCTION sp_change_event_status(
    p_id uuid, p_status text, p_scheduled_publish_at timestamptz DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE events SET
        status = p_status,
        published_at = CASE WHEN p_status = 'Published' AND published_at IS NULL THEN now() ELSE published_at END,
        scheduled_publish_at = p_scheduled_publish_at,
        updated_at = now()
    WHERE events_id = p_id;
END; $$;