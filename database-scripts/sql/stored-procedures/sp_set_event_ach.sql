-- Admin opt-in: offer ACH at checkout for this event. Only takes effect when the
-- owning tenant is ACH-enabled (developer gate); otherwise forced off. Raises if
-- the tenant is not ACH-enabled and the admin tries to turn it on.
CREATE OR REPLACE FUNCTION sp_set_event_ach(p_event_id uuid, p_enabled bool)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_tenant_ach bool;
BEGIN
    IF p_enabled THEN
        SELECT t.ach_enabled INTO v_tenant_ach
          FROM events e JOIN tenants t ON t.tenants_id = e.tenants_id
         WHERE e.events_id = p_event_id;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'Event not found' USING ERRCODE = 'P0002';
        END IF;
        IF NOT COALESCE(v_tenant_ach, false) THEN
            RAISE EXCEPTION 'ACH is not enabled for this tenant' USING ERRCODE = '22023';
        END IF;
    END IF;
    UPDATE events SET ach_enabled = p_enabled, updated_at = now()
    WHERE events_id = p_event_id;
END; $$;
