-- Developer-only platform reports. Service-fee revenue comes from bookings
-- (fee_cents on Paid/CheckedIn bookings); everything else from the
-- billing_charges ledger. Callers are developer-gated in the gRPC service.

-- Platform revenue by source within a range.
CREATE OR REPLACE FUNCTION sp_developer_revenue_by_source(p_from timestamptz, p_to timestamptz)
RETURNS TABLE(source text, revenue_cents bigint, item_count int)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT 'service_fees', COALESCE(SUM(b.fee_cents), 0)::bigint, COUNT(*)::int
      FROM bookings b
     WHERE b.status IN ('Paid','CheckedIn') AND b.created_at >= p_from AND b.created_at < p_to
    UNION ALL
    SELECT c.kind, COALESCE(SUM(c.amount_cents), 0)::bigint, COUNT(*)::int
      FROM billing_charges c
     WHERE c.created_at >= p_from AND c.created_at < p_to
     GROUP BY c.kind;
$$;

-- Revenue by tenant tier (current tier) within a range: service fees + charges.
CREATE OR REPLACE FUNCTION sp_developer_revenue_by_tier(p_from timestamptz, p_to timestamptz)
RETURNS TABLE(tier text, service_fee_cents bigint, billing_cents bigint, tenant_count int)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT t.tier,
           COALESCE(SUM(f.fees), 0)::bigint,
           COALESCE(SUM(c.charges), 0)::bigint,
           COUNT(*)::int
      FROM tenants t
      LEFT JOIN LATERAL (
          SELECT SUM(b.fee_cents) AS fees FROM bookings b
           WHERE b.tenants_id = t.tenants_id AND b.status IN ('Paid','CheckedIn')
             AND b.created_at >= p_from AND b.created_at < p_to
      ) f ON true
      LEFT JOIN LATERAL (
          SELECT SUM(ch.amount_cents) AS charges FROM billing_charges ch
           WHERE ch.tenants_id = t.tenants_id
             AND ch.created_at >= p_from AND ch.created_at < p_to
      ) c ON true
     GROUP BY t.tier;
$$;

-- Monthly revenue trend (12 buckets ending at p_to) for the dashboard chart.
CREATE OR REPLACE FUNCTION sp_developer_revenue_timeseries(p_from timestamptz, p_to timestamptz)
RETURNS TABLE(bucket_start timestamptz, service_fee_cents bigint, billing_cents bigint)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT g.bucket,
           COALESCE((SELECT SUM(b.fee_cents) FROM bookings b
                      WHERE b.status IN ('Paid','CheckedIn')
                        AND b.created_at >= g.bucket AND b.created_at < g.bucket + interval '1 month'), 0)::bigint,
           COALESCE((SELECT SUM(c.amount_cents) FROM billing_charges c
                      WHERE c.created_at >= g.bucket AND c.created_at < g.bucket + interval '1 month'), 0)::bigint
      FROM generate_series(date_trunc('month', p_from), date_trunc('month', p_to), interval '1 month') AS g(bucket);
$$;

-- Tenant activity report: key metrics per tenant, paged + searchable.
CREATE OR REPLACE FUNCTION sp_developer_tenant_activity(
    p_from timestamptz, p_to timestamptz, p_search text, p_tier text, p_offset int, p_limit int
) RETURNS TABLE(
    tenants_id uuid, name text, slug text, tier text,
    events_created int, tickets_sold int, service_fee_cents bigint,
    billing_cents bigint, avg_ticket_cents bigint, subscription_status text,
    total_count bigint
) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT t.tenants_id, t.name, t.slug, t.tier,
           COALESCE(e.cnt, 0), COALESCE(bk.tickets, 0), COALESCE(bk.fees, 0)::bigint,
           COALESCE(c.charges, 0)::bigint,
           CASE WHEN COALESCE(bk.tickets, 0) > 0
                THEN (bk.subtotal / bk.tickets)::bigint ELSE 0::bigint END,
           s.status,
           COUNT(*) OVER ()::bigint
      FROM tenants t
      LEFT JOIN LATERAL (
          SELECT COUNT(*)::int AS cnt FROM events ev
           WHERE ev.tenants_id = t.tenants_id AND ev.created_at >= p_from AND ev.created_at < p_to
      ) e ON true
      LEFT JOIN LATERAL (
          SELECT COALESCE(SUM((SELECT SUM(bl.seats) FROM booking_lines bl
                                WHERE bl.bookings_id = b.bookings_id)), 0)::int AS tickets,
                 COALESCE(SUM(b.fee_cents), 0) AS fees,
                 COALESCE(SUM(b.subtotal_cents), 0) AS subtotal
            FROM bookings b
           WHERE b.tenants_id = t.tenants_id AND b.status IN ('Paid','CheckedIn')
             AND b.created_at >= p_from AND b.created_at < p_to
      ) bk ON true
      LEFT JOIN LATERAL (
          SELECT SUM(ch.amount_cents) AS charges FROM billing_charges ch
           WHERE ch.tenants_id = t.tenants_id AND ch.created_at >= p_from AND ch.created_at < p_to
      ) c ON true
      LEFT JOIN tenant_subscriptions s
             ON s.tenants_id = t.tenants_id AND s.status IN ('trial','active','past_due')
     WHERE (p_search IS NULL OR t.name ILIKE p_search OR t.slug ILIKE p_search)
       AND (p_tier IS NULL OR t.tier = p_tier)
     ORDER BY COALESCE(bk.fees, 0) + COALESCE(c.charges, 0) DESC, t.name
    OFFSET GREATEST(p_offset, 0) LIMIT LEAST(GREATEST(p_limit, 1), 200);
$$;
