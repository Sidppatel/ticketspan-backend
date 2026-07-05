DROP FUNCTION IF EXISTS sp_list_tenants(text, boolean, integer, integer);

CREATE OR REPLACE FUNCTION sp_list_tenants(
    p_search text DEFAULT NULL,
    p_include_archived boolean DEFAULT false,
    p_offset int DEFAULT 0,
    p_limit int DEFAULT 25
) RETURNS TABLE (
    tenants_id uuid,
    slug text,
    name text,
    legal_name text,
    country_code text,
    stripe_connected_account_id text,
    stripe_charges_enabled boolean,
    stripe_payouts_enabled boolean,
    stripe_details_submitted boolean,
    stripe_onboarded_at timestamptz,
    stripe_requirements_due text,
    archived_at timestamptz,
    member_count int,
    created_at timestamptz,
    updated_at timestamptz
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_search text;
BEGIN
    v_search := NULLIF(trim(coalesce(p_search, '')), '');
    RETURN QUERY
    SELECT
        t.tenants_id,
        t.slug::text,
        t.name::text,
        t.legal_name::text,
        t.country_code::text,
        t.stripe_connected_account_id::text,
        t.stripe_charges_enabled,
        t.stripe_payouts_enabled,
        t.stripe_details_submitted,
        t.stripe_onboarded_at,
        t.stripe_requirements_due::text,
        t.archived_at,
        COALESCE(mc.cnt, 0)::int,
        t.created_at,
        t.updated_at
    FROM tenants t
    LEFT JOIN (
        SELECT u.tenants_id AS tid, count(*)::int AS cnt
        FROM users u
        WHERE u.role IN (1, 2, 3)
        GROUP BY u.tenants_id
    ) mc ON mc.tid = t.tenants_id
    WHERE (p_include_archived OR t.archived_at IS NULL)
      AND (
        v_search IS NULL
        OR t.name ILIKE '%' || v_search || '%'
        OR t.legal_name ILIKE '%' || v_search || '%'
        OR t.slug ILIKE '%' || v_search || '%'
      )
    ORDER BY t.created_at DESC
    OFFSET COALESCE(p_offset, 0)
    LIMIT  COALESCE(p_limit, 25);
END; $$;
