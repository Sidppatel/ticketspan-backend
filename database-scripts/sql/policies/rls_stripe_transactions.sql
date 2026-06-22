ALTER TABLE stripe_transactions ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON stripe_transactions;
CREATE POLICY p_tenant_isolation ON stripe_transactions
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
