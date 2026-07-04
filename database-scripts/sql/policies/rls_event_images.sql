ALTER TABLE event_images ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON event_images;
CREATE POLICY p_tenant_isolation ON event_images
    USING (app.is_developer() OR (tenants_id = app.current_tenant() AND app.can_access_event(events_id)))
    WITH CHECK (app.is_developer() OR (tenants_id = app.current_tenant() AND app.can_access_event(events_id)));
