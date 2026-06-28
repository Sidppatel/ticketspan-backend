DROP VIEW IF EXISTS vw_schedule_items CASCADE;
CREATE OR REPLACE VIEW vw_schedule_items AS
SELECT
    si.schedule_items_id AS schedule_items_id,
    si.events_id AS events_id,
    si.tenants_id AS tenants_id,
    si.title AS title,
    si.type_category AS type_category,
    si.start_time AS start_time,
    si.end_time AS end_time,
    si.created_at AS created_at,
    si.updated_at AS updated_at
FROM schedule_items si
ORDER BY si.start_time;
