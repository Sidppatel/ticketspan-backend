CREATE OR REPLACE FUNCTION sp_update_stripe_transaction_status(p_intent_id text, p_status text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE stripe_transactions SET
        status = p_status,
        paid_at = CASE WHEN p_status IN ('Succeeded','Refunded') AND paid_at IS NULL THEN now() ELSE paid_at END,
        refunded_at = CASE WHEN p_status = 'Refunded' THEN now() ELSE refunded_at END,
        updated_at = now()
    WHERE payment_intent_id = p_intent_id;
END; $$;