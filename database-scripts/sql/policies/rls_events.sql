ALTER TABLE events ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON events;
CREATE POLICY p_tenant_isolation ON events
    USING (
        app.is_developer() 
        OR (tenants_id = app.current_tenant() AND app.can_access_event(events_id))
        OR status = 'Published'
        OR (app.current_user_id() IS NOT NULL AND EXISTS (
            SELECT 1 FROM bookings b WHERE b.events_id = events.events_id AND b.users_id = app.current_user_id()
        ))
    )
    WITH CHECK (app.is_developer() OR (tenants_id = app.current_tenant() AND app.can_access_event(events_id)));
