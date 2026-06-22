ALTER TABLE venue_images ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_tenant_isolation ON venue_images;
CREATE POLICY p_tenant_isolation ON venue_images
    USING (app.is_developer() OR tenants_id = app.current_tenant())
    WITH CHECK (app.is_developer() OR tenants_id = app.current_tenant());
