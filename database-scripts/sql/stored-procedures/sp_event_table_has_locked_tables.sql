CREATE OR REPLACE FUNCTION sp_event_table_has_locked_tables(p_event_table_id uuid)
RETURNS boolean LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT EXISTS(
        SELECT 1 FROM tables
        WHERE event_tables_id = p_event_table_id
          AND status = 'Locked'
          AND lock_expires_at > now()
    );
$$;