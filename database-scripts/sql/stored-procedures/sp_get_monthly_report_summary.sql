CREATE OR REPLACE FUNCTION sp_get_monthly_report_summary(p_year int, p_month int)
RETURNS TABLE (
    total_purchases          int,
    total_charged_cents       bigint,
    total_admin_payouts_cents  bigint,
    total_platform_fees_cents  bigint,
    total_stripe_fees_cents    bigint,
    total_tax_collected_cents  bigint,
    net_platform_revenue_cents bigint
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    WITH window_bounds AS (
        SELECT
            make_timestamptz(p_year, p_month, 1, 0, 0, 0, 'UTC') AS from_ts,
            (make_timestamptz(p_year, p_month, 1, 0, 0, 0, 'UTC') + interval '1 month') AS to_ts
    ),
    src AS (
        SELECT p.*, st.total_charged_cents, st.transfer_amount_cents,
               st.stripe_fees_cents, st.tax_amount_cents, st.paid_at
        FROM purchases p
        LEFT JOIN stripe_transactions st ON st.purchases_id = p.purchases_id,
        window_bounds wb
        WHERE p.status::text IN ('Paid','CheckedIn')
          AND st.paid_at >= wb.from_ts
          AND st.paid_at <  wb.to_ts
    )
    SELECT
        COUNT(*)::int                                                                             AS total_purchases,
        COALESCE(SUM(COALESCE(total_charged_cents, total_cents))::bigint, 0)                     AS total_charged_cents,
        COALESCE(SUM(COALESCE(transfer_amount_cents, subtotal_cents))::bigint, 0)                AS total_admin_payouts_cents,
        COALESCE(SUM(fee_cents)::bigint, 0)                                                      AS total_platform_fees_cents,
        COALESCE(SUM(COALESCE(stripe_fees_cents, 0))::bigint, 0)                                  AS total_stripe_fees_cents,
        COALESCE(SUM(COALESCE(tax_amount_cents, 0))::bigint, 0)                                   AS total_tax_collected_cents,
        (COALESCE(SUM(fee_cents)::bigint, 0) - COALESCE(SUM(COALESCE(stripe_fees_cents, 0))::bigint, 0)) AS net_platform_revenue_cents
    FROM src;
$$;
