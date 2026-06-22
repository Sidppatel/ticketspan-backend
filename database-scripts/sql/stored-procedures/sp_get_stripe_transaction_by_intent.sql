CREATE OR REPLACE FUNCTION sp_get_stripe_transaction_by_intent(p_intent_id text)
RETURNS SETOF stripe_transactions
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM stripe_transactions WHERE payment_intent_id = p_intent_id;
$$;