CREATE OR REPLACE FUNCTION sp_update_event_grid(p_id uuid, p_grid_rows int, p_grid_cols int)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE events SET grid_rows = p_grid_rows, grid_cols = p_grid_cols, updated_at = now()
    WHERE events_id = p_id;
END; $$;