-- Repairs events damaged by the legacy delete-then-create bulk-edit bug in
-- PUT /admin/events/{id}. For each active tier on the event, finds the most
-- recent inactive tier with the same Label (case-insensitive) and rewrites
-- every booking that still points at the old id. Idempotent: a second call
-- is a no-op.
--
-- Returns the number of booking rows whose EventTicketTypeId was updated.
CREATE OR REPLACE FUNCTION sp_relink_orphan_ticket_types(p_event_id uuid)
RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_total int := 0;
    v_pair record;
    v_moved int;
BEGIN
    FOR v_pair IN
        SELECT new_t.event_ticket_types_id AS new_id, old_t.id AS old_id
        FROM event_ticket_types new_t
        JOIN LATERAL (
            SELECT event_ticket_types_id
              FROM event_ticket_types
              WHERE events_id = p_event_id
                AND is_active = false
                AND lower(label) = lower(new_t.label)
              ORDER BY updated_at DESC
              LIMIT 1
        ) old_t ON true
        WHERE new_t.events_id = p_event_id
          AND new_t.is_active = true
    LOOP
        UPDATE bookings
           SET event_ticket_types_id = v_pair.new_id,
               updated_at = now()
         WHERE event_ticket_types_id = v_pair.old_id;
        GET DIAGNOSTICS v_moved = ROW_COUNT;
        v_total := v_total + v_moved;
    END LOOP;

    RETURN v_total;
END; $$;
