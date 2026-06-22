CREATE OR REPLACE FUNCTION sp_delete_feedback(p_id uuid)
RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_exists boolean;
BEGIN
    SELECT EXISTS(SELECT 1 FROM feedbacks WHERE feedbacks_id = p_id) INTO v_exists;
    IF v_exists THEN
        DELETE FROM feedbacks WHERE feedbacks_id = p_id;
    END IF;
    RETURN v_exists;
END; $$;