CREATE OR REPLACE FUNCTION sp_delete_event_table(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_price uuid;
BEGIN
    SELECT prices_id INTO v_price FROM event_tables WHERE event_tables_id = p_id;
    DELETE FROM tables WHERE event_tables_id = p_id;
    DELETE FROM event_tables WHERE event_tables_id = p_id;
    -- Remove the linked Pricing Module price too (its price_rules cascade;
    -- booking_lines.prices_id is SET NULL, preserving sales history).
    IF v_price IS NOT NULL THEN
        DELETE FROM prices WHERE prices_id = v_price;
    END IF;
END; $$;
