-- Reads developer-actor (exception) entries from the unified audit_logs table. Shape
-- mirrors the old developer_logs table so DeveloperController + DeveloperLogDto keep
-- their contract. ErrorHandlingMiddleware writes exceptions with actor_type='System'
-- and event_type='Exception', packing severity/message/stack/path/method/status into
-- the metadata JSON; both paths are included via the UNION-style WHERE clause.
CREATE OR REPLACE VIEW vw_developer_logs AS
SELECT
    al.audit_logs_id                                                        AS id,
    al.created_at                                                 AS timestamp,
    COALESCE(al.metadata_json ->> 'severity', 'Error')            AS severity,
    COALESCE(al.metadata_json ->> 'message', al.action)         AS message,
    al.metadata_json ->> 'exception_type'                         AS exception_type,
    al.metadata_json ->> 'stack_trace'                            AS stack_trace,
    al.metadata_json ->> 'request_path'                           AS request_path,
    al.metadata_json ->> 'request_method'                         AS request_method,
    NULLIF(al.metadata_json ->> 'status_code', '')::int           AS status_code,
    al.actor_id                                                   AS users_id,
    al.ip                                                        AS ip_address,
    al.correlation_id::text                                       AS correlation_id,
    al.metadata_json::text                                        AS metadata_json
FROM audit_logs al
WHERE (al.actor_type = 'Developer' AND al.event_type != 'PageView')
   OR (al.actor_type = 'System' AND al.event_type = 'Exception');
