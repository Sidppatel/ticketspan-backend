CREATE OR REPLACE VIEW vw_user_profile AS
SELECT
    u.users_id AS users_id, u.email, u.first_name, u.last_name,
    u.is_active, u.last_login_at,
    u.phone, u.opt_in_location_email, u.has_completed_onboarding,
    i.storage_key AS image_storage_key, u.created_at,
    a.line1 AS address_line1,
    a.city, a.state, a.zip_code,
    u.email_verified,
    u.images_id,
    (u.google_subject IS NOT NULL) AS google_connected,
    u.bio,
    u.pronouns,
    u.preferences_json::text AS preferences_json,
    u.addresses_id,
    u.billing_addresses_id,
    ba.line1 AS billing_address_line,
    ba.city AS billing_city,
    ba.state AS billing_state,
    ba.zip_code AS billing_zip,
    u.stripe_customer_id,
    u.token_version
FROM users u
LEFT JOIN addresses a ON u.addresses_id = a.addresses_id
LEFT JOIN addresses ba ON u.billing_addresses_id = ba.addresses_id
LEFT JOIN images i ON u.images_id = i.images_id;
