CREATE OR REPLACE VIEW vw_feedbacks AS
SELECT
    f.feedbacks_id          AS feedback_id,
    f.name,
    f.email,
    f.type,
    f.message,
    f.rating,
    f.users_id,
    f.user_agent,
    f.ip_address,
    f.diagnostics::text AS diagnostics,
    f.created_at,
    CASE WHEN u.users_id IS NOT NULL
         THEN u.first_name || ' ' || u.last_name
    END              AS user_full_name
FROM feedbacks f
LEFT JOIN users u ON u.users_id = f.users_id;
