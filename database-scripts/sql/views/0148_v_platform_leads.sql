DO $$
BEGIN
    IF to_regclass('public.platform_leads') IS NOT NULL THEN
        EXECUTE $sql$
            CREATE OR REPLACE VIEW vw_platform_leads AS
            SELECT
                l.platform_leads_id,
                l.name,
                l.company_name,
                l.phone,
                l.website,
                l.description,
                l.created_at
            FROM platform_leads l
        $sql$;
        EXECUTE 'ALTER VIEW vw_platform_leads SET (security_invoker = on)';
    END IF;
END $$;
