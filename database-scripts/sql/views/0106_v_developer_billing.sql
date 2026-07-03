-- Developer billing views. Read via developer-gated RPCs; RLS on the
-- underlying tables (developer-only for billing_charges) still applies because
-- the security folder flips every view to security_invoker.

CREATE OR REPLACE VIEW vw_tenant_billing AS
SELECT
    t.tenants_id,
    t.slug,
    t.name,
    t.tier,
    t.archived_at IS NOT NULL                       AS archived,
    s.tenant_subscriptions_id,
    s.status                                        AS subscription_status,
    s.tier                                          AS subscription_tier,
    s.monthly_price_cents,
    s.current_period_end,
    s.cancel_at_period_end,
    s.pending_tier,
    s.trial_ends_at,
    ff.name                                         AS default_fee_formula_name,
    ff.percent_bps                                  AS fee_percent_bps,
    ff.flat_cents                                   AS fee_flat_cents,
    (ff.name IS NOT NULL AND ff.name NOT LIKE 'tier:%') AS has_custom_fee_override,
    (SELECT COUNT(*)::int FROM tenant_addons a
      WHERE a.tenants_id = t.tenants_id AND a.status = 'active') AS active_addons,
    (SELECT COUNT(*)::int FROM events e WHERE e.tenants_id = t.tenants_id) AS total_events
FROM tenants t
LEFT JOIN tenant_subscriptions s
       ON s.tenants_id = t.tenants_id AND s.status IN ('trial','active','past_due')
LEFT JOIN fee_formulas ff ON ff.fee_formulas_id = t.default_fee_formulas_id;

CREATE OR REPLACE VIEW vw_event_upgrades AS
SELECT
    u.event_upgrades_id,
    u.events_id,
    u.tenants_id,
    u.tier,
    u.status,
    u.price_cents,
    u.sms_credits,
    u.custom_domain_limit,
    u.refunded_cents,
    u.created_at,
    u.canceled_at,
    e.title                                         AS event_title,
    e.status                                        AS event_status,
    t.name                                          AS tenant_name,
    t.slug                                          AS tenant_slug
FROM event_upgrades u
JOIN events e ON e.events_id = u.events_id
JOIN tenants t ON t.tenants_id = u.tenants_id;

CREATE OR REPLACE VIEW vw_tenant_addons AS
SELECT
    a.tenant_addons_id,
    a.tenants_id,
    a.type,
    a.billing_period,
    a.quantity,
    a.price_cents,
    a.setup_fee_cents,
    a.status,
    a.current_period_end,
    a.usage_count,
    a.created_at,
    a.canceled_at,
    t.name                                          AS tenant_name,
    t.slug                                          AS tenant_slug
FROM tenant_addons a
JOIN tenants t ON t.tenants_id = a.tenants_id;

-- Every live fee override, tenant-level (custom default formula) and
-- event-level (events.fee_formulas_id), with the standard tier fee alongside
-- so reports can show the discount given. Developer eyes only.
CREATE OR REPLACE VIEW vw_fee_overrides AS
SELECT
    'tenant'::text                                  AS scope,
    t.tenants_id,
    t.name                                          AS tenant_name,
    t.slug                                          AS tenant_slug,
    NULL::uuid                                      AS events_id,
    NULL::text                                      AS event_title,
    ff.percent_bps,
    ff.flat_cents,
    ff.min_fee_cents,
    ff.max_fee_cents,
    std.percent_bps                                 AS standard_percent_bps,
    std.flat_cents                                  AS standard_flat_cents,
    NULL::timestamptz                               AS expires_at,
    ff.updated_at
FROM tenants t
JOIN fee_formulas ff ON ff.fee_formulas_id = t.default_fee_formulas_id
CROSS JOIN LATERAL app.tier_pricing(t.tier) std
WHERE ff.name NOT LIKE 'tier:%'
UNION ALL
SELECT
    'event',
    e.tenants_id,
    t.name,
    t.slug,
    e.events_id,
    e.title,
    ff.percent_bps,
    ff.flat_cents,
    ff.min_fee_cents,
    ff.max_fee_cents,
    std.percent_bps,
    std.flat_cents,
    e.fee_override_expires_at,
    ff.updated_at
FROM events e
JOIN tenants t ON t.tenants_id = e.tenants_id
JOIN fee_formulas ff ON ff.fee_formulas_id = e.fee_formulas_id
CROSS JOIN LATERAL app.tier_pricing(t.tier) std;
