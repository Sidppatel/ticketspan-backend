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
LANGUAGE sql STABLE
AS $$
    SELECT EXISTS (
        SELECT 1 FROM users
        WHERE users_id = app.current_user_id() AND role = 99
    );
$$;
