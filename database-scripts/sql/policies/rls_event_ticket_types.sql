ALTER TABLE event_ticket_types ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON event_ticket_types;
CREATE POLICY p_tenant_isolation ON event_ticket_types
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
