CREATE OR REPLACE FUNCTION sp_delete_event_ticket_type(p_id uuid) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE event_ticket_types SET is_active = false, updated_at = now() WHERE event_ticket_types_id = p_id;
END; $$;