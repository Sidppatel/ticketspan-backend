ALTER TABLE magic_link_tokens ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON magic_link_tokens;
CREATE POLICY p_tenant_isolation ON magic_link_tokens
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
