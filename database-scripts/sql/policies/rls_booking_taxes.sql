ALTER TABLE booking_taxes ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON booking_taxes;
CREATE POLICY p_tenant_isolation ON booking_taxes
    USING (
        app.is_developer() 
        OR tenants_id = app.current_tenant()
        OR (app.current_user_id() IS NOT NULL AND EXISTS (
            SELECT 1 FROM bookings b WHERE b.bookings_id = booking_taxes.bookings_id AND b.users_id = app.current_user_id()
        ))
    )
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
