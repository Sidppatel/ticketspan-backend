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

CREATE OR REPLACE FUNCTION app.can_access_booking(p_booking_user_id uuid, p_event_id uuid, p_tenant_id uuid)
RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER
SET search_path = public, pg_catalog
AS $$
    SELECT
        app.is_developer()
        OR (app.current_user_id() IS NOT NULL AND p_booking_user_id = app.current_user_id())
        OR (
            p_tenant_id = app.current_tenant()
            AND (
                EXISTS (
                    SELECT 1 FROM users
                    WHERE users_id = app.current_user_id() AND role IN (1, 3)
                )
                OR EXISTS (
                    SELECT 1 FROM staff_event_access
                    WHERE staff_user_id = app.current_user_id() AND event_id = p_event_id
                )
            )
        );
$$;

CREATE OR REPLACE FUNCTION app.can_access_ticket(p_booking_id uuid, p_guest_user_id uuid, p_event_id uuid, p_tenant_id uuid)
RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER
SET search_path = public, pg_catalog
AS $$
    SELECT
        app.is_developer()
        OR (app.current_user_id() IS NOT NULL AND p_guest_user_id = app.current_user_id())
        OR EXISTS (
            SELECT 1 FROM bookings b
            WHERE b.bookings_id = p_booking_id
              AND app.can_access_booking(b.users_id, p_event_id, p_tenant_id)
        );
$$;

