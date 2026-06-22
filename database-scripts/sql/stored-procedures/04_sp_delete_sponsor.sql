CREATE OR REPLACE FUNCTION sp_delete_sponsor(p_id uuid)
RETURNS void LANGUAGE plpgsql
SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM event_sponsors WHERE sponsors_id = p_id) THEN
        RAISE EXCEPTION 'sponsor_linked_to_events'
            USING ERRCODE = 'foreign_key_violation';
    END IF;
    DELETE FROM sponsors WHERE sponsors_id = p_id;
END; $$;
