-- Least-privilege runtime role. The app MUST connect as this role (not the
-- superuser/owner ep_dev) so RLS tenant-isolation policies are actually enforced.
-- Superusers and table owners bypass RLS; ep_app is neither.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ep_app') THEN
        CREATE ROLE ep_app LOGIN PASSWORD 'ep_app_password' NOSUPERUSER NOBYPASSRLS INHERIT;
    END IF;
END $$;

-- Schema access.
GRANT USAGE ON SCHEMA public, app TO ep_app;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'extensions') THEN
        EXECUTE 'GRANT USAGE ON SCHEMA extensions TO ep_app';
    END IF;
END $$;

-- Table DML on every current table.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO ep_app;
-- Sequences for serial/identity inserts.
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ep_app;
-- Execute stored procedures / helper functions (SECURITY DEFINER fns still run as owner).
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public, app TO ep_app;

-- Keep grants in place for objects created by future migrations (run as ep_dev).
ALTER DEFAULT PRIVILEGES FOR ROLE ep_dev IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ep_app;
ALTER DEFAULT PRIVILEGES FOR ROLE ep_dev IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO ep_app;
ALTER DEFAULT PRIVILEGES FOR ROLE ep_dev IN SCHEMA public
    GRANT EXECUTE ON FUNCTIONS TO ep_app;
ALTER DEFAULT PRIVILEGES FOR ROLE ep_dev IN SCHEMA app
    GRANT EXECUTE ON FUNCTIONS TO ep_app;

-- Force RLS on every RLS-enabled table so even a table owner is subject to it
-- (defensive; ep_app is not an owner but this prevents accidental bypass).
DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT c.relname
        FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relkind = 'r' AND c.relrowsecurity
    LOOP
        EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY;', r.relname);
    END LOOP;
END $$;

-- Views run with the view owner's rights by default (ep_dev → bypasses RLS).
-- security_invoker makes them run as the querying role (ep_app) so underlying
-- RLS policies apply through the view.
DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT table_name FROM information_schema.views WHERE table_schema = 'public'
    LOOP
        EXECUTE format('ALTER VIEW public.%I SET (security_invoker = on);', r.table_name);
    END LOOP;
END $$;
