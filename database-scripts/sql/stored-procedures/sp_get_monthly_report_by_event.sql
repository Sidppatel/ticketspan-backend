CREATE OR REPLACE FUNCTION sp_get_monthly_report_by_event(p_year int, p_month int)
RETURNS TABLE (
    events_id          uuid,
    event_title       varchar,
    purchase_count    int,
    charged_cents     bigint,
    admin_payout_cents bigint,
    platform_fee_cents bigint,
    stripe_fees_cents  bigint,
    tax_collected_cents bigint
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
        SELECT
            p.events_id,
            e.title AS event_title,
            p.total_cents, p.subtotal_cents, p.fee_cents,
            st.total_charged_cents, st.transfer_amount_cents,
            st.stripe_fees_cents, st.tax_amount_cents, st.paid_at
        FROM purchases p
        JOIN events e ON e.events_id = p.events_id
        LEFT JOIN stripe_transactions st ON st.purchases_id = p.purchases_id,
        window_bounds wb
        WHERE p.status::text IN ('Paid','CheckedIn')
          AND st.paid_at >= wb.from_ts
          AND st.paid_at <  wb.to_ts
    )
    SELECT
        events_id                                                                  AS events_id,
        event_title::varchar                                                       AS event_title,
        COUNT(*)::int                                                              AS purchase_count,
        COALESCE(SUM(COALESCE(total_charged_cents, total_cents))::bigint, 0)      AS charged_cents,
        COALESCE(SUM(COALESCE(transfer_amount_cents, subtotal_cents))::bigint, 0) AS admin_payout_cents,
        COALESCE(SUM(fee_cents)::bigint, 0)                                       AS platform_fee_cents,
        COALESCE(SUM(COALESCE(stripe_fees_cents, 0))::bigint, 0)                   AS stripe_fees_cents,
        COALESCE(SUM(COALESCE(tax_amount_cents, 0))::bigint, 0)                    AS tax_collected_cents
    FROM src
    GROUP BY events_id, event_title
    ORDER BY charged_cents DESC;
$$;
