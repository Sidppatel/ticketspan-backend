DO $$
DECLARE
    r record; bd record; expected int; checked int := 0; rate numeric;
BEGIN
    FOR r IN SELECT p.prices_id, p.events_id FROM prices p WHERE p.is_active LIMIT 200 LOOP
        SELECT * INTO bd FROM app.price_breakdown(r.prices_id, now(), 2, 100);
        CONTINUE WHEN bd IS NULL;
        rate := app.event_tax_rate(r.events_id);
        IF bd.selling_price_cents > 0 AND rate > 0 THEN
            expected := round((bd.selling_price_cents + bd.platform_fee_cents + bd.gateway_fee_cents) * rate)::int;
        ELSE
            expected := 0;
        END IF;
        IF bd.tax_cents <> expected THEN
            RAISE EXCEPTION 'TAX MISMATCH price % : got % expected % (sell % plat % gw % rate %)',
                r.prices_id, bd.tax_cents, expected, bd.selling_price_cents, bd.platform_fee_cents, bd.gateway_fee_cents, rate;
        END IF;
        IF bd.final_price_cents <> bd.selling_price_cents + bd.platform_fee_cents + bd.gateway_fee_cents + bd.tax_cents THEN
            RAISE EXCEPTION 'TOTAL MISMATCH price %', r.prices_id;
        END IF;
        checked := checked + 1;
    END LOOP;
    RAISE NOTICE 'OK: % prices validated (tax base = ticket + platform fee + gateway fee, venue-zip rate)', checked;
END $$;

DO $$
DECLARE bad int;
BEGIN
    SELECT count(*) INTO bad
    FROM bookings b
    JOIN booking_taxes bt ON bt.bookings_id = b.bookings_id
    WHERE bt.combined_rate > 0
      AND bt.taxable_amount_cents > 0
      AND bt.tax_amount_cents <> round(bt.taxable_amount_cents * bt.combined_rate)::int;
    IF bad > 0 THEN
        RAISE EXCEPTION 'BOOKING TAX MISMATCH on % bookings', bad;
    END IF;
    RAISE NOTICE 'OK: all booking_taxes rows internally consistent';
END $$;
