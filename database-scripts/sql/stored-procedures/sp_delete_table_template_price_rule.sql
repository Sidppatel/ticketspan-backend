CREATE OR REPLACE FUNCTION sp_delete_table_template_price_rule(p_id uuid)
RETURNS void LANGUAGE sql
    SET search_path = public, extensions, pg_catalog
AS $$
    DELETE FROM table_template_price_rules WHERE table_template_price_rules_id = p_id;
$$;
