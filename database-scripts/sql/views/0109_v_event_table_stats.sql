CREATE OR REPLACE VIEW vw_event_table_stats AS
SELECT
    events_id,
    COALESCE(SUM(total_tables), 0)::int  AS total_tables,
    COALESCE(SUM(booked_tables), 0)::int AS booked_tables
FROM vw_event_tables_summary
WHERE is_active = true
GROUP BY events_id;
