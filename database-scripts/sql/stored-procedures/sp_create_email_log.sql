CREATE OR REPLACE FUNCTION sp_create_email_log(
    p_recipient text, p_subject text, p_body text, p_status text
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO email_logs (email_logs_id, timestamp, recipient, subject, body, status)
    VALUES (gen_random_uuid(), now(), p_recipient, p_subject, p_body, p_status)
    RETURNING email_logs_id INTO v_id;
    RETURN v_id;
END; $$;