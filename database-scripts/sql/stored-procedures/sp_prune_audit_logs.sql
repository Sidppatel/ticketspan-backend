CREATE OR REPLACE FUNCTION sp_prune_audit_logs(
    p_older_than timestamptz DEFAULT NULL,
    p_only_resolved boolean DEFAULT false,
    p_event_types text[] DEFAULT ARRAY['Exception', 'Warning']
) RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_deleted int;
    v_retention_days int;
    v_cutoff timestamptz;
BEGIN
    IF p_older_than IS NOT NULL THEN
        v_cutoff := p_older_than;
    ELSE
        SELECT COALESCE(NULLIF(value, '')::int, 30) INTO v_retention_days
        FROM app_settings WHERE key = 'audit_log_retention_days';
        IF v_retention_days IS NULL THEN
            v_retention_days := 30;
        END IF;
        v_cutoff := now() - (v_retention_days || ' days')::interval;
    END IF;

    DELETE FROM audit_logs
    WHERE created_at <= v_cutoff
      AND (p_event_types IS NULL OR event_type = ANY(p_event_types))
      AND (
          NOT p_only_resolved
          OR (metadata_json->>'resolved')::boolean IS TRUE
      );

    GET DIAGNOSTICS v_deleted = ROW_COUNT;
    RETURN v_deleted;
END; $$;
