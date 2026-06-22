-- Cleans audit_logs by actor type. Legacy business_logs / developer_logs / system_logs
-- tables were dropped in the DropLegacyLogTables migration; retention is now driven by
-- ActorType so the three retention knobs (dev/admin/system) still work per category.
--   p_dev_days    → actor_type = 'Developer' (exception logs from ErrorHandlingMiddleware)
--   p_admin_days  → actor_type = 'Admin'      (business operator audit trail)
--   p_system_days → actor_type = 'System'     (platform/internal events)
-- User actor entries (actor_type = 'User') are not purged here; they follow a
-- separate retention policy documented in docs/runbooks/disaster-recovery.md.
CREATE OR REPLACE FUNCTION sp_cleanup_old_logs(
    p_dev_days int, p_admin_days int, p_system_days int
) RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_total int := 0; v_count int;
BEGIN
    DELETE FROM audit_logs
    WHERE actor_type = 'Developer'
      AND created_at < now() - (p_dev_days || ' days')::interval;
    GET DIAGNOSTICS v_count = ROW_COUNT; v_total := v_total + v_count;

    DELETE FROM audit_logs
    WHERE actor_type = 'Admin'
      AND created_at < now() - (p_admin_days || ' days')::interval;
    GET DIAGNOSTICS v_count = ROW_COUNT; v_total := v_total + v_count;

    DELETE FROM audit_logs
    WHERE actor_type = 'System'
      AND created_at < now() - (p_system_days || ' days')::interval;
    GET DIAGNOSTICS v_count = ROW_COUNT; v_total := v_total + v_count;

    RETURN v_total;
END; $$;
