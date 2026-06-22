CREATE OR REPLACE FUNCTION sp_get_purchase_info_for_event(p_event_id uuid)
RETURNS TABLE (
    tables_id       uuid,
    purchase_count int,
    seats_booked   int,
    subtotal_cents bigint
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        p.tables_id                                        AS tables_id,
        COUNT(*)::int                                      AS purchase_count,
        COALESCE(SUM(p.seats_reserved), 0)::int           AS seats_booked,
        COALESCE(SUM(p.subtotal_cents)::bigint, 0)        AS subtotal_cents
    FROM purchases p
    WHERE p.events_id = p_event_id
      AND p.tables_id IS NOT NULL
      AND p.status::text IN ('Paid','CheckedIn')
    GROUP BY p.tables_id;
$$;
