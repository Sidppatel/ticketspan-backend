ALTER TABLE event_sponsors ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON event_sponsors;
CREATE POLICY p_tenant_isolation ON event_sponsors
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
