CREATE OR REPLACE VIEW vw_invitations AS
SELECT
    i.invitations_id AS invitation_id, i.email, i.token_hash, i.role,
    i.invited_by_users_id, i.status,
    i.expires_at, i.accepted_at,
    i.created_at, i.updated_at,
    a.first_name AS inviter_first_name,
    a.last_name AS inviter_last_name
FROM invitations i
JOIN users a ON i.invited_by_users_id = a.users_id;
