CREATE OR REPLACE FUNCTION sp_enrich_stripe_transaction(
    p_intent_id text, p_total_charged_cents int, p_tax_amount_cents int, p_stripe_fees_cents int
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE stripe_transactions SET
        total_charged_cents = p_total_charged_cents,
        tax_amount_cents = p_tax_amount_cents,
        stripe_fees_cents = p_stripe_fees_cents,
        updated_at = now()
    WHERE payment_intent_id = p_intent_id;
END; $$;