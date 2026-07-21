CREATE OR REPLACE VIEW vw_developer_logs AS
SELECT
    al.audit_logs_id                                              AS id,
    al.created_at                                                 AS timestamp,
    COALESCE(al.metadata_json ->> 'severity', 'Error')            AS severity,
    COALESCE(al.metadata_json ->> 'message', al.action)           AS message,
    al.metadata_json ->> 'exception_type'                         AS exception_type,
    al.metadata_json ->> 'stack_trace'                            AS stack_trace,
    al.metadata_json ->> 'request_path'                           AS request_path,
    al.metadata_json ->> 'request_method'                         AS request_method,
    NULLIF(al.metadata_json ->> 'status_code', '')::int           AS status_code,
    al.actor_id                                                   AS users_id,
    al.ip::text                                                   AS ip_address,
    al.correlation_id::text                                       AS correlation_id,
    al.metadata_json::text                                        AS metadata_json,
    al.tenants_id                                                 AS tenants_id,
    COALESCE(al.metadata_json ->> 'source', 'backend')            AS source,
    COALESCE((al.metadata_json ->> 'resolved')::boolean, false)   AS resolved,
    al.metadata_json ->> 'resolved_notes'                         AS resolved_notes,
    al.metadata_json ->> 'resolved_by'                            AS resolved_by,
    NULLIF(al.metadata_json ->> 'resolved_at', '')::timestamptz   AS resolved_at
FROM audit_logs al
WHERE (al.actor_type = 'Developer' AND al.event_type != 'PageView')
   OR (al.actor_type = 'System' AND al.event_type IN ('Exception', 'Warning', 'Info'));
