CREATE OR REPLACE FUNCTION sp_seed_enum_definition(
    p_enum_type text, p_enum_value text, p_int_value int, p_used_in text, p_description text
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    INSERT INTO enum_definitions (enum_definitions_id, enum_type, enum_value, int_value, used_in, description, created_at, updated_at)
    VALUES (gen_random_uuid(), p_enum_type, p_enum_value, p_int_value, p_used_in, p_description, now(), now())
    ON CONFLICT (enum_type, enum_value) DO UPDATE SET
        int_value = EXCLUDED.int_value,
        used_in = EXCLUDED.used_in,
        description = EXCLUDED.description,
        updated_at = now();
END; $$;

CREATE OR REPLACE FUNCTION sp_seed_app_setting(p_key text, p_value text, p_description text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    INSERT INTO app_settings (app_settings_id, key, value, description, created_at, updated_at)
    VALUES (gen_random_uuid(), p_key, p_value, p_description, now(), now())
    ON CONFLICT (key) DO NOTHING;
END; $$;

CREATE OR REPLACE FUNCTION sp_seed_platform_defaults()
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    INSERT INTO fee_formulas (fee_formulas_id, name, percent_bps, flat_cents, is_active, created_at, updated_at)
    SELECT gen_random_uuid(), 'Standard 6% + $1.50', 600, 150, true, now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM fee_formulas WHERE name = 'Standard 6% + $1.50');

    INSERT INTO tax_rate_cache (zip_code, state, county, city, state_rate, county_rate, city_rate, local_rate, combined_rate, api_response_id, fetched_at, updated_at)
    VALUES ('36611', 'AL', 'Mobile', 'Mobile', 0.04, 0.06, 0.00, 0.00, 0.10, 'manual_injection', '2099-01-01 00:00:00+00', now())
    ON CONFLICT (zip_code) DO NOTHING;

    PERFORM sp_seed_app_setting('ai_prompt_max_length', '200', 'Maximum characters allowed for the AI event generator prompt');
END; $$;
