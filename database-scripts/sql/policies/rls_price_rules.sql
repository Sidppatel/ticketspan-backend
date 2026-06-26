ALTER TABLE price_rules ENABLE ROW LEVEL SECURITY;
ALTER TABLE price_rules FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON price_rules;
CREATE POLICY p_tenant_isolation ON price_rules
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
