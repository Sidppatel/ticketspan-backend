CREATE OR REPLACE FUNCTION sp_get_event_recent_purchases(p_event_id uuid, p_limit int)
RETURNS TABLE (
    purchases_id     uuid,
    purchase_number varchar,
    user_name       text,
    user_email      varchar,
    status         text,
    total_cents     int,
    created_at      timestamptz
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        p.purchases_id             AS purchases_id,
        p.purchase_number AS purchase_number,
        (u.first_name || ' ' || u.last_name) AS user_name,
        u.email          AS user_email,
        p.status::text   AS status,
        p.total_cents     AS total_cents,
        p.created_at      AS created_at
    FROM purchases p
    JOIN users u ON u.users_id = p.users_id
    WHERE p.events_id = p_event_id
    ORDER BY p.created_at DESC
    LIMIT p_limit;
$$;
