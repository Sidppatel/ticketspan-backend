CREATE OR REPLACE FUNCTION sp_claim_ticket(p_invite_hash text, p_guest_user_id uuid)
RETURNS TABLE(ticket_id uuid, bookings_id uuid) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    UPDATE tickets SET
        guest_users_id = p_guest_user_id, claimed_at = now(),
        status = 'Claimed', updated_at = now()
    WHERE invite_token_hash = p_invite_hash AND status = 'Invited' AND invite_expires_at > now()
    RETURNING tickets.tickets_id AS ticket_id, tickets.bookings_id;
END; $$;