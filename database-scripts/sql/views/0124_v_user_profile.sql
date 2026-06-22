CREATE OR REPLACE VIEW vw_user_profile AS
SELECT
    u.users_id AS users_id, u.email, u.first_name, u.last_name,
    u.is_active, u.last_login_at,
    u.phone, u.opt_in_location_email, u.has_completed_onboarding,
    i.storage_key AS image_storage_key, u.created_at,
    a.line1 AS address_line1,
    a.city, a.state, a.zip_code
FROM users u
LEFT JOIN addresses a ON u.addresses_id = a.addresses_id
LEFT JOIN images i ON u.images_id = i.images_id;
