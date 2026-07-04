CREATE SCHEMA IF NOT EXISTS app;

CREATE OR REPLACE FUNCTION app.current_user_id()
RETURNS uuid
LANGUAGE sql STABLE
AS $$
    SELECT NULLIF(current_setting('app.current_user_id', true), '')::uuid;
$$;

CREATE OR REPLACE FUNCTION app.current_tenant()
RETURNS uuid
LANGUAGE sql STABLE
AS $$
    SELECT NULLIF(current_setting('app.current_tenant', true), '')::uuid;
$$;

-- SECURITY DEFINER so the internal users lookup bypasses RLS. Without this the
-- users RLS policy (which calls is_developer()) recurses infinitely.
CREATE OR REPLACE FUNCTION app.is_developer()
RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER
SET search_path = public, pg_catalog
AS $$
    SELECT EXISTS (
        SELECT 1 FROM users
        WHERE users_id = app.current_user_id() AND role = 99
    );
$$;

-- Per-event authorization used by event-scoped RLS policies. Full admins (role 1)
-- and sub-tenants (role 3) see every event in their tenant, so this returns true
-- for them and the surrounding policy's tenant check does the isolation. Event
-- managers (role 4) and check-in staff (role 2) only reach events explicitly
-- granted to them via staff_event_access. SECURITY DEFINER so the users/access
-- lookups bypass RLS (the calling role only has scoped visibility).
CREATE OR REPLACE FUNCTION app.can_access_event(p_event uuid)
RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER
SET search_path = public, pg_catalog
AS $$
    SELECT
        app.is_developer()
        OR EXISTS (
            SELECT 1 FROM users
            WHERE users_id = app.current_user_id() AND role IN (1, 3)
        )
        OR EXISTS (
            SELECT 1 FROM staff_event_access
            WHERE staff_user_id = app.current_user_id() AND event_id = p_event
        );
$$;
