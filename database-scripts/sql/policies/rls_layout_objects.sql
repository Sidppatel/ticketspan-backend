ALTER TABLE layout_objects ENABLE ROW LEVEL SECURITY;
ALTER TABLE layout_objects FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON layout_objects;
CREATE POLICY p_tenant_isolation ON layout_objects
    USING (app.is_developer() OR (tenants_id = app.current_tenant() AND app.can_access_event(events_id)))
    WITH CHECK (app.is_developer() OR (tenants_id = app.current_tenant() AND app.can_access_event(events_id)));
