CREATE OR REPLACE FUNCTION sp_get_event_attendee_emails(
    p_events_id uuid
) RETURNS TABLE (
    email text,
    event_title text,
    start_date timestamptz,
    venue_name text,
    venue_address text,
    tenant_slug text,
    tenant_name text
) LANGUAGE plpgsql
    SECURITY DEFINER
    SET search_path = public, extensions, pg_catalog
AS $$
#variable_conflict use_column
BEGIN
    RETURN QUERY
    SELECT DISTINCT sub.email::text, sub.event_title::text, sub.start_date,
           sub.venue_name::text, sub.venue_address::text, sub.tenant_slug::text, sub.tenant_name::text
    FROM (
        -- Primary booking purchaser
        SELECT u.email, e.title AS event_title, e.start_date,
               COALESCE(v.name, 'Online / Venue') AS venue_name,
               COALESCE(CONCAT_WS(', ', a.line1, a.city, a.state), '') AS venue_address,
               COALESCE(t.slug, '') AS tenant_slug,
               COALESCE(t.name, 'TicketSpan') AS tenant_name
        FROM bookings b
        JOIN users u ON u.users_id = b.users_id
        JOIN events e ON e.events_id = b.events_id
        JOIN tenants t ON t.tenants_id = b.tenants_id
        LEFT JOIN venues v ON v.venues_id = e.venues_id
        LEFT JOIN addresses a ON a.addresses_id = v.addresses_id
        WHERE b.events_id = p_events_id
          AND b.status IN ('Paid', 'CheckedIn')
          AND u.email IS NOT NULL AND u.email <> ''
        
        UNION
        
        -- Invited / transferred ticket recipients from booking lines
        SELECT COALESCE(bl.invited_email, gu.email) AS email, e.title AS event_title, e.start_date,
               COALESCE(v.name, 'Online / Venue') AS venue_name,
               COALESCE(CONCAT_WS(', ', a.line1, a.city, a.state), '') AS venue_address,
               COALESCE(t.slug, '') AS tenant_slug,
               COALESCE(t.name, 'TicketSpan') AS tenant_name
        FROM booking_lines bl
        JOIN bookings b ON b.bookings_id = bl.bookings_id
        JOIN events e ON e.events_id = b.events_id
        JOIN tenants t ON t.tenants_id = b.tenants_id
        LEFT JOIN users gu ON gu.users_id = bl.guest_users_id
        LEFT JOIN venues v ON v.venues_id = e.venues_id
        LEFT JOIN addresses a ON a.addresses_id = v.addresses_id
        WHERE b.events_id = p_events_id
          AND b.status IN ('Paid', 'CheckedIn')
          AND ( (bl.invited_email IS NOT NULL AND bl.invited_email <> '') OR (gu.email IS NOT NULL AND gu.email <> '') )
    ) sub
    WHERE sub.email IS NOT NULL AND sub.email <> '';
END; $$;
