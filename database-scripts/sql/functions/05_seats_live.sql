-- Unified live-seat accounting across BOTH booking models:
--   * legacy single-line bookings   -> bookings.seats_reserved (only when the
--                                       booking has no booking_lines)
--   * multi-line cart bookings       -> booking_lines.seats
-- "Live" = Paid/CheckedIn, or Pending while its hold is still in the future.
-- These feed the oversell guards in sp_reserve_open_capacity, sp_create_multi_booking
-- and app.remaining_for_price so a tier/event cap can never be exceeded regardless
-- of which booking path sold the seats.

-- Total live seats for an event (tickets + tables), used for the event-level cap.
CREATE OR REPLACE FUNCTION app.event_seats_live(p_event uuid)
RETURNS int
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT COALESCE(SUM(s), 0)::int FROM (
        SELECT b.seats_reserved AS s
          FROM bookings b
         WHERE b.events_id = p_event
           AND b.seats_reserved IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM booking_lines bl WHERE bl.bookings_id = b.bookings_id)
           AND (b.status IN ('Paid', 'CheckedIn')
                OR (b.status = 'Pending' AND (b.hold_expires_at IS NULL OR b.hold_expires_at > now())))
        UNION ALL
        SELECT bl.seats AS s
          FROM booking_lines bl
          JOIN bookings b ON b.bookings_id = bl.bookings_id
         WHERE b.events_id = p_event
           AND (b.status IN ('Paid', 'CheckedIn')
                OR (b.status = 'Pending' AND (b.hold_expires_at IS NULL OR b.hold_expires_at > now())))
    ) x;
$$;

-- Live seats sold/held for a single ticket tier, used for the per-tier max cap
-- and remaining inventory.
CREATE OR REPLACE FUNCTION app.ticket_type_seats_live(p_type uuid)
RETURNS int
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT COALESCE(SUM(s), 0)::int FROM (
        SELECT b.seats_reserved AS s
          FROM bookings b
         WHERE b.event_ticket_types_id = p_type
           AND b.seats_reserved IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM booking_lines bl WHERE bl.bookings_id = b.bookings_id)
           AND (b.status IN ('Paid', 'CheckedIn')
                OR (b.status = 'Pending' AND (b.hold_expires_at IS NULL OR b.hold_expires_at > now())))
        UNION ALL
        SELECT bl.seats AS s
          FROM booking_lines bl
          JOIN bookings b ON b.bookings_id = bl.bookings_id
         WHERE bl.kind = 'Ticket'
           AND bl.event_ticket_types_id = p_type
           AND (b.status IN ('Paid', 'CheckedIn')
                OR (b.status = 'Pending' AND (b.hold_expires_at IS NULL OR b.hold_expires_at > now())))
    ) x;
$$;
