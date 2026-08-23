ALTER TABLE users ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON users;
CREATE POLICY p_tenant_isolation ON users
    USING (
        app.is_developer() 
        OR (tenants_id IS NOT NULL AND tenants_id = app.current_tenant())
        OR (app.current_user_id() IS NOT NULL AND users_id = app.current_user_id())
        OR (role = 0 AND tenants_id IS NULL)
    )
    WITH CHECK (
        app.is_developer() 
        OR (tenants_id IS NOT NULL AND tenants_id = app.current_tenant())
        OR (app.current_user_id() IS NOT NULL AND users_id = app.current_user_id())
        OR (role = 0 AND tenants_id IS NULL)
    );
