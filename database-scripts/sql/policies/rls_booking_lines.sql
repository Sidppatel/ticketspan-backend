ALTER TABLE booking_lines ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON booking_lines;
CREATE POLICY p_tenant_isolation ON booking_lines
    USING (app.can_access_ticket(bookings_id, guest_users_id, events_id, tenants_id))
    WITH CHECK (app.can_access_ticket(bookings_id, guest_users_id, events_id, tenants_id));

