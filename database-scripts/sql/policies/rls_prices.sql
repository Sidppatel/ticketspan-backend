ALTER TABLE prices ENABLE ROW LEVEL SECURITY;
ALTER TABLE prices FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON prices;
CREATE POLICY p_tenant_isolation ON prices
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
