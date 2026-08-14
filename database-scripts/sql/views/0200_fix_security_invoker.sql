
DO $$
DECLARE
    v text;
BEGIN
    FOREACH v IN ARRAY ARRAY[
        'vw_admin_dashboard_stats',
        'vw_business_logs',
        'vw_developer_logs',
        'vw_tenant_billing',
        'vw_event_upgrades',
        'vw_tenant_addons',
        'vw_fee_overrides',
        'vw_event_images',
        'vw_event_ticket_types_summary',
        'vw_feedbacks',
        'vw_invitations',
        'vw_tickets',
        'vw_bookings',
        'vw_system_logs',
        'vw_user_profile',
        'vw_venue_images',
        'vw_venues',
        'vw_tenants',
        'vw_stripe_transactions',
        'vw_performers',
        'vw_events',
        'vw_sponsors',
        'vw_tax_rate_cache',
        'vw_performer_public',
        'vw_sponsor_public',
        'vw_tenant_reporting_access',
        'vw_checkin_logs',
        'vw_app_settings',
        'vw_enum_definitions',
        'vw_images',
        'vw_booking_ticket_lines',
        'vw_event_ticket_types_pricing',
        'vw_booking_lines_detail',
        'vw_event_checkin_stats',
        'vw_event_guest_bookings',
        'vw_event_guest_tickets',
        'vw_fee_formulas',
        'vw_event_fee_line_items',
        'vw_event_layout_tables',
        'vw_event_table_types',
        'vw_table_templates',
        'vw_tenant_stripe_profile',
        'vw_schedule_items'
    ]
    LOOP
        IF EXISTS (
            SELECT 1 FROM pg_views
            WHERE schemaname = 'public' AND viewname = v
        ) THEN
            EXECUTE format('ALTER VIEW public.%I SET (security_invoker = on)', v);
        END IF;
    END LOOP;
END $$;
