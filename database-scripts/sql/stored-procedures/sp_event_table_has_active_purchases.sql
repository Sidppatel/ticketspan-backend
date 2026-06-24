CREATE OR REPLACE FUNCTION sp_event_table_has_active_bookings(p_event_id uuid, p_event_table_id uuid)
RETURNS boolean LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT EXISTS(
        SELECT 1 FROM bookings b
        WHERE b.events_id = p_event_id
          AND b.tables_id IS NOT NULL
          AND b.tables_id IN (SELECT tables_id FROM tables WHERE event_tables_id = p_event_table_id)
          AND b.status IN ('Paid', 'CheckedIn', 'Pending')
    );
$$;