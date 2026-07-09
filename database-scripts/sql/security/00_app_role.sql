
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ep_app') THEN
        CREATE ROLE ep_app LOGIN PASSWORD 'ep_app_password' NOSUPERUSER NOBYPASSRLS INHERIT;
    END IF;
END $$;

GRANT USAGE ON SCHEMA public, app TO ep_app;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'extensions') THEN
        EXECUTE 'GRANT USAGE ON SCHEMA extensions TO ep_app';
    END IF;
END $$;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO ep_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ep_app;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public, app TO ep_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ep_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO ep_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT EXECUTE ON FUNCTIONS TO ep_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA app
    GRANT EXECUTE ON FUNCTIONS TO ep_app;

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

DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT table_name FROM information_schema.views WHERE table_schema = 'public'
    LOOP
        EXECUTE format('ALTER VIEW public.%I SET (security_invoker = on);', r.table_name);
    END LOOP;
END $$;
