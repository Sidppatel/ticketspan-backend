CREATE OR REPLACE FUNCTION sp_revoke_ticket_invite(p_ticket_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE booking_lines SET
        status = 'Unassigned',
        ticket_code = 'TK-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8)),
        qr_token = encode(gen_random_bytes(32), 'hex'),
        invite_token_hash = NULL,
        invite_expires_at = NULL,
        invited_email = NULL,
        invite_sent_at = NULL,
        guest_users_id = NULL,
        claimed_at = NULL,
        updated_at = now()
    WHERE booking_lines_id = p_ticket_id AND kind = 'Ticket';
END; $$;
