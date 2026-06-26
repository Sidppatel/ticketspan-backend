CREATE OR REPLACE FUNCTION sp_list_table_template_price_rules(p_template_id uuid)
RETURNS TABLE(
    table_template_price_rules_id uuid, table_templates_id uuid, name text,
    rule_type text, priority int, price_cents int,
    active_from timestamptz, active_until timestamptz,
    min_remaining int, max_remaining int, is_active bool
) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT table_template_price_rules_id, table_templates_id, name,
           rule_type, priority, price_cents, active_from, active_until,
           min_remaining, max_remaining, is_active
      FROM table_template_price_rules
     WHERE table_templates_id = p_template_id
     ORDER BY priority DESC, created_at ASC;
$$;
