-- Ensure column exists on users table
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = 'users' AND column_name = 'stripe_customer_id'
    ) THEN
        ALTER TABLE users ADD COLUMN stripe_customer_id VARCHAR(255);
    END IF;
END $$;

CREATE OR REPLACE FUNCTION sp_get_or_set_user_stripe_customer(
    p_users_id uuid,
    p_stripe_customer_id text DEFAULT NULL
)
RETURNS text LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_cust text;
BEGIN
    SELECT stripe_customer_id INTO v_cust
    FROM users
    WHERE users_id = p_users_id;

    IF p_stripe_customer_id IS NOT NULL AND p_stripe_customer_id <> '' THEN
        IF v_cust IS NULL OR v_cust <> p_stripe_customer_id THEN
            UPDATE users
            SET stripe_customer_id = p_stripe_customer_id,
                updated_at = now()
            WHERE users_id = p_users_id;
            v_cust := p_stripe_customer_id;
        END IF;
    END IF;

    RETURN v_cust;
END;
$$;
