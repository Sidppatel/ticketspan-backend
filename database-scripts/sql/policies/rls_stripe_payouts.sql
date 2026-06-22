ALTER TABLE stripe_payouts ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON stripe_payouts;
CREATE POLICY p_tenant_isolation ON stripe_payouts
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
