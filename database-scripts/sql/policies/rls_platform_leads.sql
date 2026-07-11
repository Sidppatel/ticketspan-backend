DO $$
BEGIN
    IF to_regclass('public.platform_leads') IS NOT NULL THEN
        EXECUTE 'ALTER TABLE platform_leads ENABLE ROW LEVEL SECURITY';
        EXECUTE 'DROP POLICY IF EXISTS p_developer_only ON platform_leads';
        EXECUTE $sql$
            CREATE POLICY p_developer_only ON platform_leads
                USING (app.is_developer())
                WITH CHECK (app.is_developer())
        $sql$;
    END IF;
END $$;
