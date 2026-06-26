ALTER TABLE floor_plan_template_objects ENABLE ROW LEVEL SECURITY;
ALTER TABLE floor_plan_template_objects FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON floor_plan_template_objects;
CREATE POLICY p_tenant_isolation ON floor_plan_template_objects
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
