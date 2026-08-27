CREATE OR REPLACE VIEW vw_signin_developer WITH (security_invoker = true) AS
SELECT users_id, tenants_id, password_hash, pepper_version, role, email, first_name, last_name, email_verified, is_active, email_hash, token_version
FROM users
WHERE role = 99;
