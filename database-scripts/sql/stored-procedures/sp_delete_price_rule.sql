CREATE OR REPLACE FUNCTION sp_delete_price_rule(p_price_rules_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    DELETE FROM price_rules WHERE price_rules_id = p_price_rules_id;
END; $$;
