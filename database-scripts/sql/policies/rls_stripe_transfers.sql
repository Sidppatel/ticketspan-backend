ALTER TABLE stripe_transfers ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON stripe_transfers;
CREATE POLICY p_tenant_isolation ON stripe_transfers
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
