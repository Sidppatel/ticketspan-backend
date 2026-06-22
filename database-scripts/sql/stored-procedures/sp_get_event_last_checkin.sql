CREATE OR REPLACE FUNCTION sp_get_event_last_checkin(p_event_id uuid)
RETURNS timestamptz LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT MAX(updated_at)
      FROM purchases
     WHERE events_id = p_event_id
       AND status = 'CheckedIn';
$$;
