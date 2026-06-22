CREATE OR REPLACE FUNCTION sp_mark_table_booked(p_table_id uuid) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tables SET status = 'Booked', locked_by_users_id = NULL,
        lock_expires_at = NULL, updated_at = now()
    WHERE tables_id = p_table_id;
END; $$;