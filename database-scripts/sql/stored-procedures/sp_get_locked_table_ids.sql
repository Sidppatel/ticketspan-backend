CREATE OR REPLACE FUNCTION sp_get_locked_table_ids(p_event_id uuid)
RETURNS TABLE(id uuid) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT DISTINCT b.tables_id FROM purchases b
    WHERE b.events_id = p_event_id
      AND b.tables_id IS NOT NULL
      AND b.status IN ('Paid', 'CheckedIn', 'Pending')
    UNION
    SELECT t.tables_id FROM tables t
    WHERE t.events_id = p_event_id
      AND t.status = 'Locked'
      AND t.lock_expires_at > now();
$$;