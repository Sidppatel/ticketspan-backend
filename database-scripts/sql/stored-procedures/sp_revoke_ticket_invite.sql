CREATE OR REPLACE FUNCTION sp_revoke_ticket_invite(p_ticket_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE purchase_tickets SET
        status = 'Unassigned',
        invite_token_hash = NULL,
        invite_expires_at = NULL,
        invited_email = NULL,
        invite_sent_at = NULL,
        guest_users_id = NULL,
        claimed_at = NULL,
        updated_at = now()
    WHERE purchase_tickets_id = p_ticket_id;
END; $$;
