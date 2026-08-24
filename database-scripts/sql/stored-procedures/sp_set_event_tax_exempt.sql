DROP FUNCTION IF EXISTS sp_set_event_tax_exempt(uuid, bool);

CREATE OR REPLACE FUNCTION sp_set_event_tax_exempt(
    p_event_id uuid, p_exempt bool
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE events
       SET tax_exempt = p_exempt,
           updated_at = now()
     WHERE events_id = p_event_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Event not found: %', p_event_id USING ERRCODE = 'P0002';
    END IF;
END; $$;
