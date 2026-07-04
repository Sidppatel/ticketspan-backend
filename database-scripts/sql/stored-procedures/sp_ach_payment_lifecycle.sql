-- Async payment (ACH Direct Debit) lifecycle. Card confirms in seconds; ACH sits
-- in `processing` for days (T+4). Two hooks keep the seat hold correct across that
-- gap. Both keyed by Stripe PaymentIntent id, both idempotent (webhook retries).

-- payment_intent.processing: funds are on the way but not settled. Clear the hard
-- hold so sp_expire_holds (which only sweeps bookings with a non-NULL hold) leaves
-- the booking alone. Seats stay committed until the intent succeeds or fails.
CREATE OR REPLACE FUNCTION sp_mark_booking_processing(p_intent_id text)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_booking uuid;
BEGIN
    SELECT bookings_id INTO v_booking
      FROM stripe_transactions WHERE payment_intent_id = p_intent_id;
    IF v_booking IS NULL THEN
        RETURN;
    END IF;
    UPDATE bookings SET hold_expires_at = NULL, updated_at = now()
     WHERE bookings_id = v_booking AND status = 'Pending';
END; $$;

-- payment_intent.payment_failed: mark the transaction Failed. If the booking was
-- already committed (hold cleared, i.e. an ACH that had entered `processing`), its
-- seats would otherwise be stranded forever (no hold left to sweep) — so cancel it
-- to free them. A booking whose hold is still live (a card decline mid-attempt) is
-- left untouched so the buyer can retry / the sweeper can reclaim it.
CREATE OR REPLACE FUNCTION sp_fail_booking_payment(p_intent_id text)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_booking uuid; v_status text; v_hold timestamptz;
BEGIN
    UPDATE stripe_transactions SET status = 'Failed', updated_at = now()
     WHERE payment_intent_id = p_intent_id AND status NOT IN ('Succeeded', 'Refunded');

    SELECT b.bookings_id, b.status, b.hold_expires_at
      INTO v_booking, v_status, v_hold
      FROM bookings b
      JOIN stripe_transactions st ON st.bookings_id = b.bookings_id
     WHERE st.payment_intent_id = p_intent_id;

    IF v_booking IS NOT NULL AND v_status = 'Pending' AND v_hold IS NULL THEN
        PERFORM sp_cancel_booking(v_booking);
    END IF;
END; $$;
