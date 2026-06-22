ALTER TABLE sponsors ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON sponsors;
CREATE POLICY p_tenant_isolation ON sponsors
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
