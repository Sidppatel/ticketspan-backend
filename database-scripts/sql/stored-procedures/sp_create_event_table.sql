DROP FUNCTION IF EXISTS sp_create_event_table(uuid, text, int, text, text, int, int, uuid, int, int);
DROP FUNCTION IF EXISTS sp_create_event_table(uuid, text, int, text, text, int, uuid, uuid, int, int);
DROP FUNCTION IF EXISTS sp_create_event_table(uuid, text, int, text, text, int, uuid, uuid, bool, int, int, int);

-- Creates an event table TYPE and links it to a real Pricing Module price
-- (pricing_type='Table') so presale/last-minute/dynamic rules and the resolved
-- fee formula drive checkout. Admins layer rules onto the returned price via the
-- PricingService. Defaults to all-inclusive table pricing; pass
-- p_is_all_inclusive=false + p_per_attendee_cents for per-seat tables.
CREATE OR REPLACE FUNCTION sp_create_event_table(
    p_event_id uuid, p_label text, p_capacity int, p_shape text, p_color text,
    p_price_cents int, p_fee_formulas_id uuid, p_template_id uuid,
    p_is_all_inclusive bool DEFAULT true, p_per_attendee_cents int DEFAULT 0,
    p_row_span int DEFAULT NULL, p_col_span int DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid; v_prices_id uuid; v_row int; v_col int; v_event_type text; v_shape text;
BEGIN
    -- Tables belong only to Table / Both events.
    SELECT event_type INTO v_event_type FROM events WHERE events_id = p_event_id;
    IF v_event_type IS NULL THEN
        RAISE EXCEPTION 'Event not found' USING ERRCODE = 'P0002';
    END IF;
    IF v_event_type = 'Open' THEN
        RAISE EXCEPTION 'Cannot add tables to an Open-only event' USING ERRCODE = '22023';
    END IF;

    -- Grid footprint + shape: explicit override wins, else inherit the catalog
    -- template default. Shape is owned by the template (the event form only edits
    -- row/col span, capacity, price, color), so it falls back to template default,
    -- then 'Round'. Footprint falls back to 1x1.
    SELECT COALESCE(p_row_span, default_row_span, 1),
           COALESCE(p_col_span, default_col_span, 1),
           COALESCE(NULLIF(p_shape, ''), default_shape::text)
      INTO v_row, v_col, v_shape
      FROM table_templates WHERE table_templates_id = p_template_id;
    v_row := COALESCE(v_row, p_row_span, 1);
    v_col := COALESCE(v_col, p_col_span, 1);
    v_shape := COALESCE(v_shape, NULLIF(p_shape, ''), 'Round');

    v_prices_id := app.create_price(p_event_id, p_label, 'Table', p_price_cents,
        p_per_attendee_cents, p_is_all_inclusive, p_fee_formulas_id, NULL, NULL);

    INSERT INTO event_tables (tenants_id, events_id, label, capacity, shape, color,
        price_cents, fee_formulas_id, platform_fee_cents, is_active, table_templates_id,
        prices_id, row_span, col_span, created_at, updated_at)
    VALUES ((SELECT tenants_id FROM events WHERE events_id = p_event_id),
        p_event_id, p_label, p_capacity, v_shape, p_color,
        p_price_cents, p_fee_formulas_id, app.compute_fee(p_price_cents, p_fee_formulas_id),
        true, p_template_id,
        v_prices_id, v_row, v_col, now(), now())
    RETURNING event_tables_id INTO v_id;

    -- Snapshot catalog template price rules onto the new event price. Admins may
    -- then override per event via the PricingService rule CRUD.
    IF p_template_id IS NOT NULL THEN
        INSERT INTO price_rules (tenants_id, prices_id, name, rule_type, priority,
            price_cents, active_from, active_until, min_remaining, max_remaining,
            is_active, created_at, updated_at)
        SELECT ttr.tenants_id, v_prices_id, ttr.name, ttr.rule_type, ttr.priority,
            ttr.price_cents, ttr.active_from, ttr.active_until,
            ttr.min_remaining, ttr.max_remaining, true, now(), now()
          FROM table_template_price_rules ttr
         WHERE ttr.table_templates_id = p_template_id
           AND ttr.is_active = true;
    END IF;

    RETURN v_id;
END; $$;
