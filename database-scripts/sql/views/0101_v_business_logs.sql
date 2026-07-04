-- Reads admin-actor entries from the unified audit_logs table, joining users
-- for the email/role display columns. Legacy business_logs table dropped in
-- 20260424_DropLegacyLogTables migration; shape preserved so existing SP consumers
-- and C# BusinessLogView entity keep compiling.
CREATE OR REPLACE VIEW vw_business_logs AS
SELECT
    al.audit_logs_id                                   AS id,
    al.created_at                            AS timestamp,
    al.action                               AS action,
    al.actor_id                              AS users_id,
    au.email                                AS business_user_email,
    au.role                                 AS business_user_role,
    al.subject_type                          AS entity_type,
    al.subject_id                            AS entity_id,
    NULLIF(al.metadata_json ->> 'description', '') AS description,
    al.metadata_json::text                   AS metadata_json,
    al.ip                                   AS ip_address,
    al.events_id                             AS events_id
FROM audit_logs al
LEFT JOIN users au ON au.users_id = al.actor_id
WHERE al.actor_type = 'Admin'
  AND al.event_type != 'PageView';
