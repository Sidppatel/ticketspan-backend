ALTER TABLE table_templates ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON table_templates;
CREATE POLICY p_tenant_isolation ON table_templates
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
