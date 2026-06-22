-- Read projection for the users table. StripeConnectedAccountId
-- is no longer included — Connect data was migrated to tenants as
-- part of the Stripe Connect Express rollout. Callers needing acct ids
-- join tenants directly via users.tenants_id.
CREATE OR REPLACE VIEW vw_users AS
SELECT
    au.users_id AS users_id, au.email, au.email_hash, au.first_name, au.last_name,
    au.role, au.is_active, au.last_login_at,
    i.storage_key AS image_storage_key,
    au.phone, au.created_at, au.updated_at
FROM users au
LEFT JOIN images i ON au.images_id = i.images_id;
