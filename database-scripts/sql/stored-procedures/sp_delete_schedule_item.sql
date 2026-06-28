DROP FUNCTION IF EXISTS sp_delete_schedule_item(uuid);

CREATE OR REPLACE FUNCTION sp_delete_schedule_item(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    DELETE FROM schedule_items WHERE schedule_items_id = p_id;
END; $$;
