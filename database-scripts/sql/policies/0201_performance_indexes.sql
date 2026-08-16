CREATE INDEX IF NOT EXISTS ix_events_tenant_status_start 
ON events (tenants_id, status, start_date DESC);

CREATE INDEX IF NOT EXISTS ix_bookings_events_status_created 
ON bookings (events_id, status, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_bookings_users_status_created 
ON bookings (users_id, status, created_at DESC);
