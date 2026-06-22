CREATE OR REPLACE FUNCTION sp_lock_table(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_hold_minutes int
) RETURNS TABLE(id uuid, label text, lock_expires_at timestamptz) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    UPDATE tables SET
        status = 'Locked', locked_by_users_id = p_user_id,
        lock_expires_at = now() + (p_hold_minutes || ' minutes')::interval,
        updated_at = now()
    WHERE tables.tables_id = p_table_id AND tables.events_id = p_event_id
      AND tables.is_active = true
      AND (
          tables.status = 'Available'
          OR (tables.status = 'Locked' AND tables.lock_expires_at <= now())
      )
    RETURNING tables.tables_id, tables.label::text, tables.lock_expires_at;
END; $$;