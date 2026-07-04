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

-- Per-event authorization used by event-scoped RLS policies. Only the event-scoped
-- roles are restricted: check-in staff (2) and event managers (4) reach an event
-- only when it is granted via staff_event_access. EVERYONE ELSE — anonymous public
-- viewers, attendees (0), admins (1), sub-tenants (3), developers — is unrestricted
-- here, so the surrounding policy's tenant check (and public status filters) do the
-- isolation. Must stay permissive for the no-user case or the public site sees no
-- events. SECURITY DEFINER so the users/access lookups bypass RLS.
CREATE OR REPLACE FUNCTION app.can_access_event(p_event uuid)
RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER
SET search_path = public, pg_catalog
AS $$
    SELECT
        app.is_developer()
        OR EXISTS (
            SELECT 1 FROM events
            WHERE events_id = p_event AND status = 'Published'
        )
        OR NOT EXISTS (
            SELECT 1 FROM users
            WHERE users_id = app.current_user_id() AND role IN (2, 4)
        )
        OR EXISTS (
            SELECT 1 FROM staff_event_access
            WHERE staff_user_id = app.current_user_id() AND event_id = p_event
        );
$$;
