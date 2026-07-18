ALTER TABLE addresses ENABLE ROW LEVEL SECURITY;
ALTER TABLE addresses FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS p_universal_access ON addresses;
CREATE POLICY p_universal_access ON addresses
    USING (true)
    WITH CHECK (true);
