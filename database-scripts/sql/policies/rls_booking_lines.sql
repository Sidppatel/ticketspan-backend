ALTER TABLE booking_lines ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON booking_lines;
CREATE POLICY p_tenant_isolation ON booking_lines
    USING (app.is_developer() OR (tenants_id = app.current_tenant() AND app.can_access_event(events_id)))
    WITH CHECK (app.is_developer() OR (tenants_id = app.current_tenant() AND app.can_access_event(events_id)));
