DROP FUNCTION IF EXISTS sp_create_event_ticket_type(uuid, text, int, int, int, int, text);
DROP FUNCTION IF EXISTS sp_create_event_ticket_type(uuid, text, int, uuid, int, int, text);

-- Creates an open-seating ticket tier and links it to a real Pricing Module price
-- (pricing_type='TicketTier') so each tier (VIP / GA / Early Bird) carries its own
-- presale/last-minute/dynamic rules and resolves server-side at checkout. The fee
-- itself is resolved via app.compute_fee and cached in platform_fee_cents.
CREATE OR REPLACE FUNCTION sp_create_event_ticket_type(
    p_event_id uuid, p_label text, p_price_cents int,
    p_fee_formulas_id uuid, p_max_quantity int, p_sort_order int,
    p_description text DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid; v_prices_id uuid; v_event_type text;
BEGIN
    -- Open ticket tiers belong only to Open / Both events.
    SELECT event_type INTO v_event_type FROM events WHERE events_id = p_event_id;
    IF v_event_type IS NULL THEN
        RAISE EXCEPTION 'Event not found' USING ERRCODE = 'P0002';
    END IF;
    IF v_event_type = 'Table' THEN
        RAISE EXCEPTION 'Cannot add ticket tiers to a Table-only event' USING ERRCODE = '22023';
    END IF;

    v_prices_id := app.create_price(p_event_id, p_label, 'TicketTier', p_price_cents,
        0, false, p_fee_formulas_id, NULL, p_max_quantity);

    INSERT INTO event_ticket_types (tenants_id, events_id, label, price_cents,
        fee_formulas_id, platform_fee_cents, prices_id,
        max_quantity, sort_order, description, is_active, created_at, updated_at)
    VALUES ((SELECT tenants_id FROM events WHERE events_id = p_event_id),
        p_event_id, p_label, p_price_cents,
        p_fee_formulas_id, app.compute_fee(p_price_cents, p_fee_formulas_id), v_prices_id,
        p_max_quantity, p_sort_order, p_description, true, now(), now())
    RETURNING event_ticket_types_id INTO v_id;
    RETURN v_id;
END; $$;
