CREATE OR REPLACE FUNCTION sp_upsert_setting(
    p_key text, p_value text, p_description text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    INSERT INTO app_settings (app_settings_id, key, value, description, created_at, updated_at)
    VALUES (gen_random_uuid(), p_key, p_value, p_description, now(), now())
    ON CONFLICT (key) DO UPDATE SET
        value = EXCLUDED.value,
        description = COALESCE(EXCLUDED.description, app_settings.description),
        updated_at = now();
END; $$;