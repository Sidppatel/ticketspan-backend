ALTER TABLE floor_plan_template_tables ENABLE ROW LEVEL SECURITY;
ALTER TABLE floor_plan_template_tables FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON floor_plan_template_tables;
CREATE POLICY p_tenant_isolation ON floor_plan_template_tables
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
