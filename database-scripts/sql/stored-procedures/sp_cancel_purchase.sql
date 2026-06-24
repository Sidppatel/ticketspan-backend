CREATE OR REPLACE FUNCTION sp_cancel_booking(p_booking_id uuid) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE bookings SET status = 'Cancelled', updated_at = now()
    WHERE bookings_id = p_booking_id;

    UPDATE tables SET status = 'Available', locked_by_users_id = NULL,
        lock_expires_at = NULL, updated_at = now()
    WHERE booking_tables_id IN (SELECT tables_id FROM booking_tables WHERE bookings_id = p_booking_id)
      AND status IN ('Locked', 'Booked');
END; $$;