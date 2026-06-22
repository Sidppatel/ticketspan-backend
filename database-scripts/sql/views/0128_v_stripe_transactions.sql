CREATE OR REPLACE VIEW vw_stripe_transactions AS
SELECT
    st.stripe_transactions_id AS transaction_id,
    st.payment_intent_id,
    st.status,
    st.amount_cents,
    st.currency,
    st.paid_at,
    st.refunded_at,
    st.refund_id,
    st.transfer_amount_cents,
    st.stripe_fees_cents,
    st.total_charged_cents,
    st.created_at,
    p.purchases_id AS purchases_id,
    p.purchase_number,
    p.status AS purchase_status,
    e.events_id AS events_id,
    e.title AS event_title,
    u.users_id AS users_id,
    u.email AS user_email,
    u.first_name AS user_first_name,
    u.last_name AS user_last_name,
    bu.tenants_id AS tenants_id
FROM stripe_transactions st
JOIN purchases p ON p.purchases_id = st.purchases_id
JOIN events e ON e.events_id = p.events_id
JOIN users u ON u.users_id = p.users_id
JOIN users bu ON bu.users_id = e.created_by_users_id;
