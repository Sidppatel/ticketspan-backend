# Stored Procedure Migration Map

166 SPs total. Conventions for rewrite: snake_case columns, `p_users_id`/`p_tenants_id` params, `users` (unified), `tenants`, `vw_*` view refs, pepper_version on password writes, `SECURITY DEFINER` on pre-auth lookups (signup/signin/exists/by-email).

## Verification harness
```
docker run -d --name svyne_pg_test -e POSTGRES_USER=ep_dev -e POSTGRES_PASSWORD=ep_dev_password -e POSTGRES_DB=event_platform -p 55432:5432 postgres:16
cd database-scripts/Db
DATABASE_URL="Host=localhost;Port=55432;Database=event_platform;Username=ep_dev;Password=ep_dev_password" dotnet ef database update --no-build
# then pipe Sql/Security, Sql/Policies, Sql/Procedures/*.sql via psql
```

## Done + DB-verified (10)
sp_signup_user (now takes p_tenants_id, p_pepper_version, p_role), sp_signin_user_google (p_tenants_id, p_role), sp_get_user_by_id, sp_get_user_by_email_for_signin, sp_get_user_by_email_hash, sp_set_user_password (p_pepper_version), sp_update_user_last_login, sp_user_exists_by_email, sp_list_users, sp_set_user_active.

## DROP (merged business/user duplicates → user equivalents)
sp_get_business_user_by_id→sp_get_user_by_id; sp_get_business_user_by_email→sp_get_user_by_email_hash; sp_create_business_user→sp_signup_user; sp_business_user_exists_by_email→sp_user_exists_by_email; sp_update_business_user_password→sp_set_user_password; sp_update_business_user_last_login→sp_update_user_last_login; sp_set_business_user_avatar_image→sp_set_user_avatar_image; sp_clear_business_user_avatar_image→sp_clear_user_avatar_image; sp_create_business_user_device_session→sp_create_device_session; sp_create_business_user_password_reset_token / sp_get_business_user_password_reset_token / sp_invalidate_business_user_password_reset_token → unified password_reset_tokens SPs; sp_revoke_all_business_user_sessions→sp_revoke_all_user_sessions; sp_increment_business_user_failed_login / sp_reset_business_user_lockout → sp_increment_user_failed_login / sp_reset_user_lockout (rename param). sp_update_business_user→sp_update_user_profile (merge).

## RENAME (organization → tenant)
sp_create_organization→sp_create_tenant (must also insert first admin user role=1 + magic link); sp_update_organization→sp_update_tenant; sp_archive_organization→sp_archive_tenant; sp_count_organizations→sp_count_tenants; sp_list_organizations→sp_list_tenants; sp_get_organization_by_business_user→sp_get_tenant_by_user; sp_get_organization_members→sp_get_tenant_members; sp_add_business_user_to_organization→sp_add_user_to_tenant; sp_remove_business_user_from_organization→sp_remove_user_from_tenant; sp_get_organization_stripe_status / sp_update_organization_stripe_account / sp_clear_organization_stripe_account / sp_update_organization_stripe_status → tenant variants.

## RENAME (business_user_event → user_event)
sp_assign_business_user_event→sp_assign_user_event; sp_unassign_business_user_event→sp_unassign_user_event; sp_business_user_event_exists→sp_user_event_exists; sp_list_staff_for_event / sp_list_events_for_staff / sp_staff_can_access_event → keep names, point at user_events + users(role IN (2,3)).

## MECHANICAL (snake_case + tenants_id, ~110)
All event/venue/performer/sponsor/table/purchase/ticket/image/stripe/feedback/log/invitation/magic-link/token/device-session SPs. Pending. Each must add tenants_id to inserts and snake_case all columns; reads still return base table or move to vw_* where applicable.

## Done + DB-verified (tenant cluster, 11)
sp_create_tenant (tenant + first admin role=1 + magic link; returns out_tenants_id, out_users_id), sp_update_tenant, sp_archive_tenant (deactivates member users), sp_count_tenants, sp_list_tenants (member_count = users role IN(1,2,3)), sp_get_tenant_by_user, sp_get_tenant_members, sp_get_tenant_stripe_status, sp_update_tenant_stripe_account, sp_clear_tenant_stripe_account, sp_update_tenant_stripe_status.

Model change: membership = users.tenants_id (no re-parenting). Dropped sp_add/remove_business_user_to/from_organization. CK forbids non-dev user with null tenant, so "remove member" = deactivate (sp_set_user_active) not detach.

## Test container (current): svyne_pg_test2, db svyne_test, user svyne_app, pw svyne_test_pw_2026, port 55433.

## Done + DB-verified (session/profile/auth-merge, 22)
sp_update_user_profile, sp_set_user_avatar_image (fn sp_set_user_image), sp_clear_user_avatar_image (fn sp_clear_user_image), sp_delete_user, sp_user_counts, sp_upsert_user (now p_tenants_id,p_role), sp_get_user_by_email, sp_create_device_session, sp_revoke_device_session, sp_revoke_all_user_sessions, sp_update_session_activity (updates users.last_request_at), sp_cleanup_expired_sessions, sp_increment_user_failed_login, sp_reset_user_lockout, sp_update_user (admin; role smallint), sp_assign_user_event, sp_unassign_user_event, sp_user_event_exists, sp_create_password_reset_token, sp_get_password_reset_token, sp_consume_password_reset_token, sp_invalidate_password_reset_token.

Deleted business_user dups (19 files): *_business_user_* merged into user equivalents above. Unified password_reset_tokens table replaces business+user reset token SPs.

Note for event SP batch: events.is_featured is NOT NULL with no DB default — sp_create_event must set it (or add default).

## Status: 43 / 166 rewritten+verified. Remaining ~110 mechanical: events, venues, performers, sponsors, tables/event_tables, purchases/tickets, images (event/venue/platform), stripe txns/transfers/payouts, feedback, logs/audit, invitations, magic_link, email_verification, dashboard, checkin, layout, search.
