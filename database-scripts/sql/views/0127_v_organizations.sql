CREATE OR REPLACE VIEW vw_tenants AS
SELECT
    o.tenants_id AS tenants_id,
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
    COALESCE(rev.total, 0)::bigint AS total_revenue_cents
FROM tenants o
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM users bu WHERE bu.tenants_id = o.tenants_id
) mc ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt 
    FROM events e 
    JOIN users bu ON bu.users_id = e.created_by_users_id
    WHERE bu.tenants_id = o.tenants_id
) ec ON true
LEFT JOIN LATERAL (
    SELECT SUM(p.subtotal_cents)::bigint AS total
    FROM bookings p
    JOIN events e ON e.events_id = p.events_id
    JOIN users bu ON bu.users_id = e.created_by_users_id
    WHERE bu.tenants_id = o.tenants_id
      AND p.status IN ('Paid', 'CheckedIn')
) rev ON true;
