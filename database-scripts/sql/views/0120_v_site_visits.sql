-- Creates a read-only view of site page views / visits, joining audit_logs with user details.
CREATE OR REPLACE VIEW vw_site_visits AS
SELECT
    al.audit_logs_id                                                        AS id,
    al.created_at                                                 AS timestamp,
    al.action                                                    AS path,
    al.ip                                                        AS ip_address,
    al.metadata_json ->> 'userAgent'                              AS user_agent,
    al.metadata_json ->> 'referrer'                               AS referrer,
    al.metadata_json ->> 'screenResolution'                       AS screen_resolution,
    al.metadata_json ->> 'portal'                                 AS portal,
    al.metadata_json ->> 'browser'                                AS browser,
    al.metadata_json ->> 'os'                                     AS os,
    CASE WHEN al.actor_type = 'User' THEN al.actor_id ELSE NULL END AS users_id,
    CASE WHEN al.actor_type IN ('Admin', 'Developer') THEN al.actor_id ELSE NULL END AS admin_users_id,
    COALESCE(u.email, bu.email)                               AS user_email,
    COALESCE(u.first_name || ' ' || u.last_name, bu.first_name || ' ' || bu.last_name) AS user_full_name,
    COALESCE(al.actor_type::text, 'Anonymous')                    AS user_role
FROM audit_logs al
LEFT JOIN users u ON al.actor_type = 'User' AND al.actor_id = u.users_id
LEFT JOIN users bu ON al.actor_type IN ('Admin', 'Developer') AND al.actor_id = bu.users_id
WHERE al.event_type = 'PageView';
