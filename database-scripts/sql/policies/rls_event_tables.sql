ALTER TABLE event_tables ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON event_tables;
CREATE POLICY p_tenant_isolation ON event_tables
    USING (app.is_developer() OR (tenants_id = app.current_tenant() AND app.can_access_event(events_id)))
    WITH CHECK (app.is_developer() OR (tenants_id = app.current_tenant() AND app.can_access_event(events_id)));
