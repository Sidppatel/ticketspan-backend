ALTER TABLE bookings ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON bookings;
CREATE POLICY p_tenant_isolation ON bookings
    USING (app.can_access_booking(users_id, events_id, tenants_id))
    WITH CHECK (app.can_access_booking(users_id, events_id, tenants_id));

