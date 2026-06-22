CREATE OR REPLACE FUNCTION sp_list_users()
RETURNS SETOF users
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM users ORDER BY created_at;
$$;
