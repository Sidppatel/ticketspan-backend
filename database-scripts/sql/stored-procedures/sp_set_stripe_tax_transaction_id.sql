CREATE OR REPLACE FUNCTION sp_set_stripe_tax_transaction_id(p_intent_id text, p_tax_transaction_id text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE stripe_transactions SET
        tax_transaction_id = p_tax_transaction_id,
        updated_at = now()
    WHERE payment_intent_id = p_intent_id;
END; $$;