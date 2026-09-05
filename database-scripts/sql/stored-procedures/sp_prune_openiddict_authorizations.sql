CREATE OR REPLACE FUNCTION sp_prune_openiddict_authorizations(
    p_older_than timestamptz DEFAULT NULL
) RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_deleted int;
    v_retention_days int;
    v_cutoff timestamptz;
BEGIN
    IF p_older_than IS NOT NULL THEN
        v_cutoff := p_older_than;
    ELSE
        SELECT COALESCE(NULLIF(value, '')::int, 7) INTO v_retention_days
        FROM app_settings WHERE key = 'openiddict_authorization_retention_days';
        IF v_retention_days IS NULL THEN
            v_retention_days := 7;
        END IF;
        v_cutoff := now() - (v_retention_days || ' days')::interval;
    END IF;

    DELETE FROM "OpenIddictAuthorizations" a
    WHERE (a.status IN ('revoked', 'inactive') AND a.creation_date <= v_cutoff)
       OR (a.type = 'adhoc' AND a.creation_date <= v_cutoff AND NOT EXISTS (
           SELECT 1 FROM "OpenIddictTokens" t WHERE t.authorization_id = a.id
       ));

    GET DIAGNOSTICS v_deleted = ROW_COUNT;
    RETURN v_deleted;
END; $$;
