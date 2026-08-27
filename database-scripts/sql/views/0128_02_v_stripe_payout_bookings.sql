CREATE OR REPLACE VIEW vw_stripe_payout_bookings AS
SELECT
    b.bookings_id,
    st.payment_intent_id,
    t.stripe_connected_account_id,
    b.subtotal_cents,
    b.fee_cents,
    b.tax_cents,
    COALESCE(t.tax_collection_mode::text, 'platform') AS tax_collection_mode
FROM bookings b
JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
JOIN tenants t ON t.tenants_id = b.tenants_id;
