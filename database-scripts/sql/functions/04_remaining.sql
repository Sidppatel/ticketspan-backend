-- Live remaining inventory for a Price, feeding the min_remaining/max_remaining
-- gates on price_rules (presale caps, low-stock surcharges, etc). Returns NULL
-- when the sellable is effectively unlimited, which app.resolve_price treats as
-- "skip the inventory gates".
--   Table prices : count of Available individual tables of the linked table type
--   TicketTier/AddOn : configured cap minus seats already sold or actively held
CREATE OR REPLACE FUNCTION app.remaining_for_price(p_prices_id uuid)
RETURNS int
LANGUAGE plpgsql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_type text; v_max int; v_remaining int; v_sold int;
BEGIN
    SELECT pricing_type, max_quantity INTO v_type, v_max
      FROM prices WHERE prices_id = p_prices_id;
    IF NOT FOUND THEN RETURN NULL; END IF;

    IF v_type = 'Table' THEN
        SELECT count(*) INTO v_remaining
          FROM tables t
          JOIN event_tables et ON et.event_tables_id = t.event_tables_id
         WHERE et.prices_id = p_prices_id
           AND t.is_active = true
           AND t.status = 'Available';
        RETURN v_remaining;
    END IF;

    -- TicketTier / AddOn: fall back to the linked ticket type's cap when the
    -- price itself sets none.
    IF v_max IS NULL THEN
        SELECT max_quantity INTO v_max
          FROM event_ticket_types WHERE prices_id = p_prices_id LIMIT 1;
    END IF;
    IF v_max IS NULL THEN RETURN NULL; END IF;  -- unlimited

    SELECT COALESCE(SUM(b.seats_reserved), 0) INTO v_sold
      FROM bookings b
      JOIN event_ticket_types ett ON ett.event_ticket_types_id = b.event_ticket_types_id
     WHERE ett.prices_id = p_prices_id
       AND (b.status = 'Paid'
            OR (b.status = 'Pending'
                AND (b.hold_expires_at IS NULL OR b.hold_expires_at > now())));

    v_remaining := GREATEST(v_max - v_sold, 0);
    RETURN v_remaining;
END; $$;
