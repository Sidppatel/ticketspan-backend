-- Drop prior grid-based signatures (5-arg and 6-arg with grid dims).
DROP FUNCTION IF EXISTS sp_save_event_layout(uuid, int, int, jsonb, uuid[]);
DROP FUNCTION IF EXISTS sp_save_event_layout(uuid, int, int, jsonb, uuid[], jsonb);

-- Pixel-canvas layout save: upsert the request set of tables + objects (by Id),
-- delete the rest. No event grid bounds anymore (free canvas).
CREATE OR REPLACE FUNCTION sp_save_event_layout(
    p_event_id uuid, p_tables jsonb, p_locked_ids uuid[], p_objects jsonb DEFAULT '[]'::jsonb
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_request_ids uuid[];
    v_object_ids uuid[];
    v_table jsonb;
    v_obj jsonb;
    v_id uuid;
    v_tenant uuid;
BEGIN
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_event_id;

    SELECT COALESCE(array_agg(NULLIF(t->>'Id', '')::uuid) FILTER (WHERE NULLIF(t->>'Id', '') IS NOT NULL), '{}')
    INTO v_request_ids
    FROM jsonb_array_elements(p_tables) AS t;

    DELETE FROM tables
    WHERE events_id = p_event_id
      AND tables_id <> ALL(v_request_ids)
      AND tables_id <> ALL(p_locked_ids);

    FOR v_table IN SELECT * FROM jsonb_array_elements(p_tables)
    LOOP
        v_id := NULLIF(v_table->>'Id', '')::uuid;
        IF v_id IS NOT NULL AND v_id = ANY(p_locked_ids) THEN
            CONTINUE;
        END IF;

        IF v_id IS NOT NULL AND EXISTS(SELECT 1 FROM tables WHERE tables_id = v_id) THEN
            UPDATE tables SET
                label = v_table->>'Label',
                pos_x = (v_table->>'PosX')::numeric,
                pos_y = (v_table->>'PosY')::numeric,
                is_active = (v_table->>'IsActive')::bool,
                sort_order = (v_table->>'SortOrder')::int,
                event_tables_id = (v_table->>'EventTableId')::uuid,
                width = COALESCE((v_table->>'Width')::numeric, 80),
                height = COALESCE((v_table->>'Height')::numeric, 80),
                shape_override = NULLIF(v_table->>'ShapeOverride', ''),
                color_override = NULLIF(v_table->>'ColorOverride', ''),
                capacity_override = NULLIF(v_table->>'CapacityOverride', '')::int,
                updated_at = now()
            WHERE tables_id = v_id;
        ELSE
            INSERT INTO tables (tables_id, tenants_id, events_id, event_tables_id, label,
                pos_x, pos_y, is_active, sort_order, status,
                width, height, shape_override, color_override, capacity_override,
                created_at, updated_at)
            VALUES (
                COALESCE(v_id, gen_random_uuid()), v_tenant, p_event_id,
                (v_table->>'EventTableId')::uuid,
                v_table->>'Label',
                (v_table->>'PosX')::numeric,
                (v_table->>'PosY')::numeric,
                (v_table->>'IsActive')::bool,
                (v_table->>'SortOrder')::int,
                'Available',
                COALESCE((v_table->>'Width')::numeric, 80),
                COALESCE((v_table->>'Height')::numeric, 80),
                NULLIF(v_table->>'ShapeOverride', ''),
                NULLIF(v_table->>'ColorOverride', ''),
                NULLIF(v_table->>'CapacityOverride', '')::int,
                now(), now()
            );
        END IF;
    END LOOP;

    -- Layout objects (Entry / Exit / Stage): upsert the request set, delete the rest.
    SELECT COALESCE(array_agg(NULLIF(o->>'Id', '')::uuid) FILTER (WHERE NULLIF(o->>'Id', '') IS NOT NULL), '{}')
    INTO v_object_ids
    FROM jsonb_array_elements(p_objects) AS o;

    DELETE FROM layout_objects
    WHERE events_id = p_event_id
      AND layout_objects_id <> ALL(v_object_ids);

    FOR v_obj IN SELECT * FROM jsonb_array_elements(p_objects)
    LOOP
        v_id := NULLIF(v_obj->>'Id', '')::uuid;
        IF v_id IS NOT NULL AND EXISTS(SELECT 1 FROM layout_objects WHERE layout_objects_id = v_id) THEN
            UPDATE layout_objects SET
                object_type = v_obj->>'ObjectType',
                label = NULLIF(v_obj->>'Label', ''),
                pos_x = (v_obj->>'PosX')::numeric,
                pos_y = (v_obj->>'PosY')::numeric,
                width = COALESCE((v_obj->>'Width')::numeric, 80),
                height = COALESCE((v_obj->>'Height')::numeric, 80),
                color = NULLIF(v_obj->>'Color', ''),
                sort_order = COALESCE((v_obj->>'SortOrder')::int, 0),
                updated_at = now()
            WHERE layout_objects_id = v_id;
        ELSE
            INSERT INTO layout_objects (layout_objects_id, tenants_id, events_id, object_type,
                label, pos_x, pos_y, width, height, color, sort_order,
                created_at, updated_at)
            VALUES (
                COALESCE(v_id, gen_random_uuid()), v_tenant, p_event_id,
                v_obj->>'ObjectType',
                NULLIF(v_obj->>'Label', ''),
                (v_obj->>'PosX')::numeric,
                (v_obj->>'PosY')::numeric,
                COALESCE((v_obj->>'Width')::numeric, 80),
                COALESCE((v_obj->>'Height')::numeric, 80),
                NULLIF(v_obj->>'Color', ''),
                COALESCE((v_obj->>'SortOrder')::int, 0),
                now(), now()
            );
        END IF;
    END LOOP;
END; $$;
