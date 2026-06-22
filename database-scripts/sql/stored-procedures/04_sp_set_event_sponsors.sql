CREATE OR REPLACE FUNCTION sp_set_event_sponsors(p_event_id uuid, p_links jsonb)
RETURNS void LANGUAGE plpgsql
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_tenant uuid;
BEGIN
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_event_id;
    DELETE FROM event_sponsors WHERE events_id = p_event_id;
    INSERT INTO event_sponsors (tenants_id, events_id, sponsors_id, sort_order, event_meta, created_at)
    SELECT
        v_tenant,
        p_event_id,
        (link->>'sponsorId')::uuid,
        COALESCE((link->>'sortOrder')::int, 0),
        COALESCE(link->'eventMeta', '[]'::jsonb),
        now()
    FROM jsonb_array_elements(COALESCE(p_links, '[]'::jsonb)) link;
END; $$;
