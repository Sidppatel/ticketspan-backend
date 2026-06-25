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
