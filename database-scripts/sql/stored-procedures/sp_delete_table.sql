CREATE OR REPLACE FUNCTION sp_delete_table(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    DELETE FROM tables WHERE tables_id = p_id;
END; $$;