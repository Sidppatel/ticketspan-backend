CREATE OR REPLACE VIEW vw_tenants AS
SELECT
    o.tenants_id AS tenants_id,
    o.slug,
    o.name,
    o.legal_name,
    o.country_code,
    o.stripe_connected_account_id,
    o.stripe_charges_enabled,
    o.stripe_payouts_enabled,
    o.stripe_details_submitted,
    o.stripe_onboarded_at,
    o.created_at,
    o.archived_at,
    COALESCE(mc.cnt, 0)::int AS member_count,
    COALESCE(ec.cnt, 0)::int AS event_count,
    COALESCE(rev.total, 0)::bigint AS total_revenue_cents,
    o.ach_enabled
FROM tenants o
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM users bu WHERE bu.tenants_id = o.tenants_id AND bu.role IN (1, 2, 3, 4)
) mc ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt
    FROM events e
    WHERE e.tenants_id = o.tenants_id
) ec ON true
LEFT JOIN LATERAL (
    SELECT SUM(p.subtotal_cents)::bigint AS total
    FROM bookings p
    JOIN events e ON e.events_id = p.events_id
    WHERE e.tenants_id = o.tenants_id
      AND p.status IN ('Paid', 'CheckedIn')
) rev ON true;
