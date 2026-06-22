CREATE OR REPLACE FUNCTION sp_get_purchase_stats(
    p_business_user_ids uuid[],
    p_event_id uuid
)
RETURNS TABLE (
    total     int,
    paid      int,
    checked_in int,
    revenue   bigint
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        COUNT(*)::int                                                                                  AS total,
        COUNT(*) FILTER (WHERE p.status::text IN ('Paid','CheckedIn'))::int                          AS paid,
        COUNT(*) FILTER (WHERE p.status::text = 'CheckedIn')::int                                    AS checked_in,
        COALESCE(SUM(p.subtotal_cents) FILTER (WHERE p.status::text IN ('Paid','CheckedIn')), 0)::bigint AS revenue
    FROM purchases p
    JOIN events e ON e.events_id = p.events_id
    WHERE (p_business_user_ids IS NULL OR e.created_by_users_id = ANY(p_business_user_ids))
      AND (p_event_id IS NULL OR p.events_id = p_event_id);
$$;
