CREATE OR REPLACE FUNCTION fn_merge_sponsor_meta(p_defaults jsonb, p_overrides jsonb)
RETURNS jsonb LANGUAGE sql IMMUTABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT COALESCE(
        jsonb_agg(
            item
            ORDER BY (item->>'sortOrder')::int NULLS LAST, item->>'key'
        ),
        '[]'::jsonb
    )
    FROM (
        SELECT m AS item
        FROM jsonb_array_elements(COALESCE(p_overrides, '[]'::jsonb)) m
        WHERE (m->>'value') IS NOT NULL AND length(m->>'value') > 0
        UNION ALL
        SELECT d AS item
        FROM jsonb_array_elements(COALESCE(p_defaults, '[]'::jsonb)) d
        WHERE NOT EXISTS (
            SELECT 1
            FROM jsonb_array_elements(COALESCE(p_overrides, '[]'::jsonb)) o
            WHERE (o->>'key') = (d->>'key')
        )
    ) merged;
$$;
