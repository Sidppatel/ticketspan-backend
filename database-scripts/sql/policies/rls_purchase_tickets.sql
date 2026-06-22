ALTER TABLE purchase_tickets ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON purchase_tickets;
CREATE POLICY p_tenant_isolation ON purchase_tickets
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
