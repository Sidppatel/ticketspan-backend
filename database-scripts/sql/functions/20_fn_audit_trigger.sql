-- Row-level audit trigger emitting entity-change events into the unified audit_logs
-- table. Actor and tenant are read from the per-connection GUCs that Db.OpenAsync
-- sets from the caller's JWT (app.current_user_id / app.current_tenant), so every
-- write is attributed to who made it without any handler-side logging code. The
-- subject id is the table's PK column, which the model names <table>_id. A soft
-- delete (is_active true -> false) is recorded as a Delete, not an Update, so the
-- audit trail reads as the intent. Bound below to the pricing tables.
CREATE OR REPLACE FUNCTION fn_audit_trigger() RETURNS trigger LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_action text;
    v_subject_id uuid;
    v_events_id uuid;
    v_before jsonb;
    v_after  jsonb;
    v_actor uuid := nullif(current_setting('app.current_user_id', true), '')::uuid;
    v_tenant uuid := nullif(current_setting('app.current_tenant', true), '')::uuid;
    v_actor_type text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_action := 'Delete';
        v_before := to_jsonb(OLD);
        v_after  := NULL;
    ELSIF TG_OP = 'UPDATE' THEN
        v_before := to_jsonb(OLD);
        v_after  := to_jsonb(NEW);
        IF (v_before ->> 'is_active') = 'true' AND (v_after ->> 'is_active') = 'false' THEN
            v_action := 'Delete';
        ELSE
            v_action := 'Update';
        END IF;
    ELSIF TG_OP = 'INSERT' THEN
        v_action := 'Insert';
        v_before := NULL;
        v_after  := to_jsonb(NEW);
    ELSE
        RETURN NULL;
    END IF;

    v_subject_id := (coalesce(v_after, v_before) ->> (TG_TABLE_NAME || '_id'))::uuid;
    v_events_id := nullif(coalesce(v_after, v_before) ->> 'events_id', '')::uuid;

    v_actor_type := CASE
        WHEN v_actor IS NULL THEN 'System'
        WHEN v_tenant IS NULL THEN 'Developer'
        ELSE 'Admin'
    END;

    INSERT INTO audit_logs (
        audit_logs_id, tenants_id, created_at, event_type, actor_type, actor_id,
        subject_type, subject_id, events_id, action, metadata_json, ip, correlation_id
    )
    VALUES (
        gen_random_uuid(), v_tenant, now(), 'EntityChange', v_actor_type, v_actor,
        TG_TABLE_NAME, v_subject_id, v_events_id, v_action,
        jsonb_build_object('before', v_before, 'after', v_after, 'source', TG_TABLE_NAME),
        NULL, NULL
    );

    IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
END; $$;

DROP TRIGGER IF EXISTS tr_audit_prices ON prices;
CREATE TRIGGER tr_audit_prices AFTER INSERT OR UPDATE OR DELETE ON prices
    FOR EACH ROW EXECUTE FUNCTION fn_audit_trigger();

DROP TRIGGER IF EXISTS tr_audit_price_rules ON price_rules;
CREATE TRIGGER tr_audit_price_rules AFTER INSERT OR UPDATE OR DELETE ON price_rules
    FOR EACH ROW EXECUTE FUNCTION fn_audit_trigger();
