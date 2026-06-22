-- Idempotent insert of a Stripe Connect transfer event into the
-- stripe_transfers audit table. Resolves OrganizationId from the destination
-- account the_id; resolves PurchaseId by joining stripe_transactions on the
-- payment intent extracted from the source transaction (when present).
--
-- Returns the row the_id (existing or new) so callers can log it. ON CONFLICT
-- DO NOTHING + COALESCE(returning, lookup) keeps the call safe under Stripe
-- webhook retries.
CREATE OR REPLACE FUNCTION sp_insert_stripe_transfer(
    p_stripe_transfer_id text,
    p_stripe_account_id text,
    p_payment_intent_id text,
    p_amount_cents int,
    p_currency text,
    p_raw_event jsonb
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_org_id uuid;
    v_purchase_id uuid;
    v_id uuid;
BEGIN
    SELECT the_id INTO v_org_id
    FROM tenants
    WHERE stripe_connected_account_id = p_stripe_account_id;

    IF v_org_id IS NULL THEN
        RAISE EXCEPTION 'No organization found with Stripe account %', p_stripe_account_id
            USING ERRCODE = 'no_data_found';
    END IF;

    -- Best-effort purchase resolution. Stripe Connect transfers carry the
    -- source charge / payment intent on the event; we hop through
    -- stripe_transactions to land on the platform purchase.
    IF p_payment_intent_id IS NOT NULL THEN
        SELECT purchases_id INTO v_purchase_id
        FROM stripe_transactions
        WHERE payment_intent_id = p_payment_intent_id;
    END IF;

    INSERT INTO stripe_transfers (
        stripe_transfers_id, stripe_transfer_id, tenants_id, purchases_id,
        amount_cents, currency, raw_event,
        created_at, updated_at
    )
    VALUES (
        gen_random_uuid(), p_stripe_transfer_id, v_org_id, v_purchase_id,
        p_amount_cents, COALESCE(p_currency, 'usd'), p_raw_event,
        now(), now()
    )
    ON CONFLICT (stripe_transfer_id) DO NOTHING
    RETURNING stripe_transfers_id INTO v_id;

    -- ON CONFLICT skipped insert; pull existing row stripe_transfers_id so the caller still
    -- has a stable reference to log.
    IF v_id IS NULL THEN
        SELECT stripe_transfers_id INTO v_id
        FROM stripe_transfers
        WHERE stripe_transfer_id = p_stripe_transfer_id;
    END IF;

    RETURN v_id;
END; $$;
