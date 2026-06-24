CREATE OR REPLACE FUNCTION sp_create_stripe_transaction(
    p_booking_id uuid, p_intent_id text, p_amount_cents int,
    p_transfer_amount_cents int DEFAULT NULL, p_tax_calculation_id text DEFAULT NULL,
    p_currency text DEFAULT 'usd'
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO stripe_transactions (tenants_id, bookings_id, payment_intent_id, status,
        amount_cents, transfer_amount_cents, tax_calculation_id, currency, created_at, updated_at)
    VALUES ((SELECT tenants_id FROM bookings WHERE bookings_id = p_booking_id),
        p_booking_id, p_intent_id, 'RequiresConfirmation',
        p_amount_cents, p_transfer_amount_cents, p_tax_calculation_id, p_currency, now(), now())
    RETURNING stripe_transactions_id INTO v_id;
    RETURN v_id;
END; $$;
