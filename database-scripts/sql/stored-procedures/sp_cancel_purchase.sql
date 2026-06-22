CREATE OR REPLACE FUNCTION sp_cancel_purchase(p_purchase_id uuid) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE purchases SET status = 'Cancelled', updated_at = now()
    WHERE purchases_id = p_purchase_id;

    UPDATE tables SET status = 'Available', locked_by_users_id = NULL,
        lock_expires_at = NULL, updated_at = now()
    WHERE purchase_tables_id IN (SELECT tables_id FROM purchase_tables WHERE purchases_id = p_purchase_id)
      AND status IN ('Locked', 'Booked');
END; $$;