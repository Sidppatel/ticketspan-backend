-- Row-level audit trigger emitting entity-change events into the unified audit_logs
-- table (actor_type = 'System'). Not currently bound to any table; kept available
-- for opt-in wiring via CREATE TRIGGER ... EXECUTE FUNCTION fn_audit_trigger() on
-- mutation-heavy tables where row-level provenance is required.
CREATE OR REPLACE FUNCTION fn_audit_trigger() RETURNS trigger LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_action text;
    v_subject_id uuid;
    v_before jsonb;
    v_after  jsonb;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_action := 'Delete';
        v_subject_id := (OLD.id)::uuid;
        v_before := to_jsonb(OLD);
        v_after  := NULL;
    ELSIF TG_OP = 'UPDATE' THEN
        v_action := 'Update';
        v_subject_id := (NEW.id)::uuid;
        v_before := to_jsonb(OLD);
        v_after  := to_jsonb(NEW);
    ELSIF TG_OP = 'INSERT' THEN
        v_action := 'Insert';
        v_subject_id := (NEW.id)::uuid;
        v_before := NULL;
        v_after  := to_jsonb(NEW);
    ELSE
        RETURN NULL;
    END IF;

    INSERT INTO audit_logs (
        audit_logs_id, created_at, event_type, actor_type, actor_id,
        subject_type, subject_id, action, metadata_json, ip, correlation_id
    )
    VALUES (
        gen_random_uuid(), now(), 'EntityChange', 'System', NULL,
        TG_TABLE_NAME, v_subject_id, v_action,
        jsonb_build_object('before', v_before, 'after', v_after, 'source', TG_TABLE_NAME),
        NULL, NULL
    );

    IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
END; $$;
