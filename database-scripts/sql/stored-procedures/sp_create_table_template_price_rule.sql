-- Adds a catalog-level price rule to a table template. Snapshotted into per-event
-- price_rules when an event table is created from the template (see
-- sp_create_event_table). Tenant is derived from the template.
CREATE OR REPLACE FUNCTION sp_create_table_template_price_rule(
    p_template_id uuid, p_name text, p_rule_type text, p_priority int,
    p_price_cents int, p_active_from timestamptz, p_active_until timestamptz,
    p_min_remaining int, p_max_remaining int
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO table_template_price_rules (tenants_id, table_templates_id, name,
        rule_type, priority, price_cents, active_from, active_until,
        min_remaining, max_remaining, is_active, created_at, updated_at)
    VALUES ((SELECT tenants_id FROM table_templates WHERE table_templates_id = p_template_id),
        p_template_id, p_name, COALESCE(p_rule_type, 'TimeWindow'), COALESCE(p_priority, 0),
        p_price_cents, p_active_from, p_active_until,
        p_min_remaining, p_max_remaining, true, now(), now())
    RETURNING table_template_price_rules_id INTO v_id;
    RETURN v_id;
END; $$;
