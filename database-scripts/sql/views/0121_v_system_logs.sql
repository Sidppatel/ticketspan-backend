-- Reads system-actor entries from the unified audit_logs table. Legacy system_logs
-- table dropped in DropLegacyLogTables migration; shape preserved so existing
-- consumers (sp_get_system_logs, SystemLogView entity, DeveloperController) keep
-- compiling.
CREATE OR REPLACE VIEW vw_system_logs AS
SELECT
    al.audit_logs_id                                                        AS id,
    al.created_at                                                 AS timestamp,
    COALESCE(al.metadata_json ->> 'category', 'EntityChange')     AS category,
    al.action                                                    AS action,
    al.metadata_json ->> 'source'                                 AS source,
    al.subject_type                                               AS entity_type,
    al.subject_id                                                 AS entity_id,
    al.metadata_json ->> 'before'                                 AS before_json,
    al.metadata_json ->> 'after'                                  AS after_json,
    al.actor_id                                                   AS users_id,
    au.email                                                     AS user_email,
    au.role                                                      AS user_role,
    al.correlation_id::text                                       AS correlation_id,
    NULLIF(al.metadata_json ->> 'duration_ms', '')::bigint        AS duration_ms,
    al.metadata_json::text                                        AS metadata_json
FROM audit_logs al
LEFT JOIN users au ON au.users_id = al.actor_id
WHERE al.actor_type = 'System';
