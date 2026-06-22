CREATE OR REPLACE FUNCTION sp_delete_event(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    DELETE FROM events WHERE events_id = p_id;
END; $$;