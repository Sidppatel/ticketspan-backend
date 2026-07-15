CREATE OR REPLACE FUNCTION sp_enrich_stripe_transaction(
    p_intent_id text, 
    p_total_charged_cents int, 
    p_stripe_fees_cents int, 
    p_method_type text DEFAULT NULL, 
    p_method_last4 text DEFAULT NULL,
    p_method_brand text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE stripe_transactions SET
        total_charged_cents = p_total_charged_cents,
        stripe_fees_cents = p_stripe_fees_cents,
        payment_method_type = COALESCE(p_method_type, payment_method_type),
        payment_method_last4 = COALESCE(p_method_last4, payment_method_last4),
        payment_method_brand = COALESCE(p_method_brand, payment_method_brand),
        updated_at = now()
    WHERE payment_intent_id = p_intent_id;
END; $$;