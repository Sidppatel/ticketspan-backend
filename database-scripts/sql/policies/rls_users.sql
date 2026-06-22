ALTER TABLE users ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON users;
CREATE POLICY p_tenant_isolation ON users
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
