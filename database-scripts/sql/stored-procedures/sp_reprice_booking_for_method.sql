DROP FUNCTION IF EXISTS sp_reprice_booking_for_method(uuid, uuid, text);

-- Re-price a Pending booking for a chosen payment method WITHOUT touching the
-- immutable selling-price snapshot. Only the fee leg is swapped:
--   'card' -> service fee (resolved formula) + gateway fee (today's behavior)
--   'ach'  -> flat ACH fee replaces the service fee, gateway suppressed
-- Because the ACH fee is smaller than the service fee, the buyer's total drops;
-- baseline_total_cents (the card total) lets the UI show the savings. Fees are
-- server-authoritative — the client never supplies amounts.
--
-- Guards match sp_get_booking_for_payment: booking must exist, be owned by the
-- caller, be Pending, and hold must still be live. 'ach' is rejected unless the
-- tenant is ACH-enabled AND the event opted in.
CREATE OR REPLACE FUNCTION sp_reprice_booking_for_method(
    p_booking_id uuid, p_user_id uuid, p_method text
) RETURNS TABLE(
    subtotal_cents int,
    fee_cents int,
    total_cents int,
    baseline_total_cents int
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_status text; v_owner uuid; v_hold timestamptz;
    v_tenant uuid; v_event uuid;
    v_ach_ok boolean;
    v_svc_formula uuid; v_gw_formula uuid; v_ach_formula uuid;
    v_line record;
    v_platform int; v_gateway int;
    v_card_platform int; v_card_gateway int;
    v_sub int := 0; v_fee int := 0; v_total int := 0; v_baseline int := 0;
BEGIN
    IF p_method NOT IN ('card', 'ach') THEN
        RAISE EXCEPTION 'Unknown payment method %', p_method USING ERRCODE = '22023';
    END IF;

    SELECT b.status, b.users_id, b.hold_expires_at, b.tenants_id, b.events_id
      INTO v_status, v_owner, v_hold, v_tenant, v_event
      FROM bookings b
     WHERE b.bookings_id = p_booking_id
     FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Booking not found' USING ERRCODE = 'P0002';
    END IF;
    IF v_owner <> p_user_id THEN
        RAISE EXCEPTION 'Booking does not belong to caller' USING ERRCODE = '42501';
    END IF;
    IF v_status <> 'Pending' THEN
        RAISE EXCEPTION 'Booking is not payable (status %)', v_status USING ERRCODE = '22023';
    END IF;
    IF v_hold IS NOT NULL AND v_hold <= now() THEN
        RAISE EXCEPTION 'Booking hold has expired' USING ERRCODE = '22023';
    END IF;

    SELECT t.ach_enabled AND e.ach_enabled
      INTO v_ach_ok
      FROM tenants t JOIN events e ON e.events_id = v_event
     WHERE t.tenants_id = v_tenant;

    IF p_method = 'ach' AND COALESCE(v_ach_ok, false) = false THEN
        RAISE EXCEPTION 'ACH is not available for this event' USING ERRCODE = '22023';
    END IF;

    v_gw_formula := app.resolve_gateway_formula(v_tenant);
    v_ach_formula := app.resolve_ach_formula(v_tenant);

    -- Re-fee every priced line (skip the zero-price child seats of a table).
    FOR v_line IN
        SELECT bl.booking_lines_id, bl.selling_price_cents, p.fee_formulas_id AS explicit_formula
          FROM booking_lines bl
          LEFT JOIN prices p ON p.prices_id = bl.prices_id
         WHERE bl.bookings_id = p_booking_id
           AND bl.selling_price_cents > 0
    LOOP
        v_svc_formula := app.resolve_fee_formula(v_line.explicit_formula, v_event, v_tenant);

        -- Card baseline (for savings), always computed.
        v_card_platform := app.compute_fee(v_line.selling_price_cents, v_svc_formula);
        v_card_gateway := app.compute_fee(v_line.selling_price_cents + v_card_platform, v_gw_formula);

        IF p_method = 'ach' THEN
            v_platform := app.compute_fee(v_line.selling_price_cents, v_ach_formula);
            v_gateway := 0;
        ELSE
            v_platform := v_card_platform;
            v_gateway := v_card_gateway;
        END IF;

        UPDATE booking_lines
           SET platform_fee_cents = v_platform,
               gateway_fee_cents = v_gateway,
               fee_cents = v_platform + v_gateway,
               total_cents = selling_price_cents + v_platform + v_gateway,
               final_price_cents = selling_price_cents + v_platform + v_gateway,
               updated_at = now()
         WHERE booking_lines_id = v_line.booking_lines_id;

        v_sub := v_sub + v_line.selling_price_cents;
        v_fee := v_fee + v_platform + v_gateway;
        v_total := v_total + v_line.selling_price_cents + v_platform + v_gateway;
        v_baseline := v_baseline + v_line.selling_price_cents + v_card_platform + v_card_gateway;
    END LOOP;

    UPDATE bookings
       SET subtotal_cents = v_sub, fee_cents = v_fee, total_cents = v_total, updated_at = now()
     WHERE bookings_id = p_booking_id;

    subtotal_cents := v_sub;
    fee_cents := v_fee;
    total_cents := v_total;
    baseline_total_cents := v_baseline;
    RETURN NEXT;
END; $$;
