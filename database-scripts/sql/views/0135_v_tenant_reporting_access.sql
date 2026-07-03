DROP VIEW IF EXISTS vw_tenant_reporting_access CASCADE;

CREATE VIEW vw_tenant_reporting_access AS
SELECT
    t.tenants_id,
    t.slug::text AS slug,
    t.name::text AS name,
    t.tier::text AS tier,
    t.advanced_reporting_enabled,
    (t.tier IN ('professional','business','enterprise') OR t.advanced_reporting_enabled) AS has_advanced_reporting,
    t.archived_at IS NOT NULL AS archived
FROM tenants t;
