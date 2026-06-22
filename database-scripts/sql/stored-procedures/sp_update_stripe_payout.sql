-- Upsert one Stripe payout row keyed on the Stripe payout id. Called from
-- both payout.created (status='pending'|'in_transit') and payout.paid
-- (status='paid' + PaidAt) — the SP hides the create vs update split.
--
-- Resolves OrganizationId from the source Stripe account the_id. If the org is
-- unknown the SP raises so the webhook handler can clear the dedupe key and
-- let Stripe retry once the org is wired up.
CREATE OR REPLACE FUNCTION sp_update_stripe_payout(
    p_stripe_payout_id text,
    p_stripe_account_id text,
    p_amount_cents int,
    p_currency text,
    p_status text,
    p_arrival_date timestamptz,
    p_paid_at timestamptz,
    p_raw_event jsonb
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_org_id uuid;
    v_id uuid;
BEGIN
    SELECT the_id INTO v_org_id
    FROM tenants
    WHERE stripe_connected_account_id = p_stripe_account_id;

    IF v_org_id IS NULL THEN
        RAISE EXCEPTION 'No organization found with Stripe account %', p_stripe_account_id
            USING ERRCODE = 'no_data_found';
    END IF;

    INSERT INTO stripe_payouts (
        stripe_payouts_id, stripe_payout_id, tenants_id,
        amount_cents, currency, status,
        arrival_date, paid_at, raw_event,
        created_at, updated_at
    )
    VALUES (
        gen_random_uuid(), p_stripe_payout_id, v_org_id,
        p_amount_cents, COALESCE(p_currency, 'usd'), p_status,
        p_arrival_date, p_paid_at, p_raw_event,
        now(), now()
    )
    ON CONFLICT (stripe_payout_id) DO UPDATE
    SET status      = EXCLUDED.status,
        -- Never overwrite PaidAt once set — payout.paid is final.
        paid_at      = COALESCE(stripe_payouts.paid_at, EXCLUDED.paid_at),
        arrival_date = COALESCE(EXCLUDED.arrival_date, stripe_payouts.arrival_date),
        raw_event    = EXCLUDED.raw_event,
        updated_at   = now()
    RETURNING stripe_payouts_id INTO v_id;

    RETURN v_id;
END; $$;
