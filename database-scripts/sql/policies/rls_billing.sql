-- Billing tables. Developers manage everything; tenants may read their own
-- subscription and add-ons (their assigned tier/features), but never
-- billing_charges or other tenants' rows. Fee overrides stay invisible to
-- tenants (vw_fee_overrides reads tenants/events/fee_formulas which they can
-- see, but the developer-gated RPC is the only consumer).

ALTER TABLE tenant_subscriptions ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_subscriptions FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_dev_all ON tenant_subscriptions;
CREATE POLICY p_dev_all ON tenant_subscriptions
    USING (app.is_developer())
    WITH CHECK (app.is_developer());
DROP POLICY IF EXISTS p_tenant_read ON tenant_subscriptions;
CREATE POLICY p_tenant_read ON tenant_subscriptions FOR SELECT
    USING (tenants_id = app.current_tenant());

ALTER TABLE event_upgrades ENABLE ROW LEVEL SECURITY;
ALTER TABLE event_upgrades FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_dev_all ON event_upgrades;
CREATE POLICY p_dev_all ON event_upgrades
    USING (app.is_developer())
    WITH CHECK (app.is_developer());
DROP POLICY IF EXISTS p_tenant_read ON event_upgrades;
CREATE POLICY p_tenant_read ON event_upgrades FOR SELECT
    USING (tenants_id = app.current_tenant());

ALTER TABLE tenant_addons ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_addons FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_dev_all ON tenant_addons;
CREATE POLICY p_dev_all ON tenant_addons
    USING (app.is_developer())
    WITH CHECK (app.is_developer());
DROP POLICY IF EXISTS p_tenant_read ON tenant_addons;
CREATE POLICY p_tenant_read ON tenant_addons FOR SELECT
    USING (tenants_id = app.current_tenant());

-- Financial ledger: developer-only, both directions.
ALTER TABLE billing_charges ENABLE ROW LEVEL SECURITY;
ALTER TABLE billing_charges FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_dev_only ON billing_charges;
CREATE POLICY p_dev_only ON billing_charges
    USING (app.is_developer())
    WITH CHECK (app.is_developer());
