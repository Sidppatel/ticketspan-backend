# Phase 1 Progress Tracker

Branch: `restructure/phase1-unified-schema`. Convention: full snake_case columns, PK/FK `<table>s_id`. Zero comments (Rule 1). `api` project intentionally breaks until Phase 2.

## Done
- `Tenant` entity (replaces `Organization`): adds `Slug`.
- Unified `User` entity: `TenantsId?`, `Role short`, `PepperVersion`, merged attendee+admin fields.
- Deleted: `BusinessUser`, `Organization`, `BusinessPasswordResetToken`, `BusinessUserEvent`, enums `AdminRole`/`UserRole`.
- `PasswordResetToken` (unified, `UsersId`) replaces business+user reset tokens.
- `UserEvent` (replaces `BusinessUserEvent`): `UsersId`, `EventsId`, `AssignedByUsersId`.
- `DeviceSession`: single `UsersId` (dropped dual user/business FK).
- `Invitation`: `TenantsId?`, `Role short`, `InvitedByUsersId`.
- `StripeTransfer` / `StripePayout`: `OrganizationId` → `TenantsId`.
- `UserEmailVerificationToken`: `UsersId`.
- `Event`: added `TenantsId`, owner `CreatedByUsersId` (was `BusinessUserId`).

## Done (cont.)
- Added `TenantsId` to all tenant-scoped entities (Venue, TableTemplate, Performer, Sponsor, EventTable, EventTicketType, Table, Purchase, PurchaseTicket, PurchaseTable, StripeTransaction, Image?, EventImage, VenueImage, Feedback?, EmailLog?, AuditLog?, MagicLinkToken?, EventPerformer, EventSponsor). Nullable on Image/Feedback/EmailLog/AuditLog/MagicLink. Global/no-tenant: addresses, app_settings, platform_images.
- Stripped all comments (Rule 1) from rewritten entities. Plural table FKs (`events_id`, `users_id`, etc.).
- Added `EFCore.NamingConventions` 10.0.1 → `UseSnakeCaseNamingConvention()` in DesignTimeDbContextFactory. Global snake_case columns.
- Rewrote `EventPlatformDbContext.cs`: Tenant + unified User configs (UNIQUE(tenants_id,email,role); CHECK (role=99)=(tenants_id IS NULL); CHECK role IN(...); unique google_subject partial; pepper_version default 1; email_hash non-unique index). All check constraints snake-cased. Generic loop maps every table PK `Id`→`<table>_id` (Rule 10). Views remapped `v_*`→`vw_*`.
- Renamed view entities: OrganizationView→TenantView, BusinessUserView→UserView (role short), BusinessUserEventView→UserEventView; InvitationView role→short, InvitedByUserId.
- **Db project builds 0 warnings / 0 errors.**
- Reset migrations: deleted old 5, kept `MigrationSqlLoader`, generated clean `InitialCreate` (snake_case unified schema verified). Added `CREATE EXTENSION IF NOT EXISTS pgcrypto`.

## Done (SQL block)
- RLS: `app.current_user_id()`/`app.current_tenant()`/`app.is_developer()` in `Sql/Security/00_app_security_functions.sql`; 25 per-table policy files in `Sql/Policies/rls_*.sql`. **All apply clean to real PG.**
- 10 core auth/user SPs rewritten + DB-verified (see SP_MIGRATION_MAP.md).
- Added DB defaults: PK `gen_random_uuid()` (generic loop), users.failed_login_attempts/opt_in_location_email/has_completed_onboarding.
- **Migration applies to fresh Postgres 16; signup + Google sign-in + both negative constraints verified end-to-end.**
- Verification harness documented in SP_MIGRATION_MAP.md (docker pg + ef database update).

## PHASE 1 COMPLETE (verified)
- All 170 SP files + 31 views + RLS install clean. From-scratch `dotnet ef database update` (InitialCreate + InstallSqlArtifacts) builds full DB via .NET only (Rule 15).
- Converter pipeline: conv.pl (dquote->snake, table/FK renames, v_->vw_), pass2.pl (alias.id->table_id), pass3.pl (statement-aware bare id). Then manual fixes for tenant_id inserts (25 SPs derive/param), owner col (events.created_by_users_id), role smallint, RETURNING ambiguity.
- DB-verified end-to-end: tenant+admin+magiclink, venue, event, event_table, ticket_type, table, performer+link, sponsor+link, event_image, signup, seated purchase+confirm (Paid,1 ticket), open-capacity reserve+confirm (Paid,2 tickets), vw_events jsonb perfs+spons, sp_get_purchase_stats.
- Counts: 34 tables, 31 vw_, 168 sp/fn, 25 policies, 3 app fns.
- InstallSqlArtifacts migration loads Sql.{Security,Views,ViewsOrg,Performers,Sponsors,Procedures,ProceduresOrg,ProceduresStripe,Policies}.

## REMAINING (Phase 2/3)
- Phase 2: protos/ contracts, gRPC host, rebuild api service/repo layer (currently won't compile - references deleted BusinessUser/Organization), auth (pepper+google+jwt), tenant/RLS middleware, remove REST controllers.
- Phase 3: move SQL out of csproj to /database-scripts/{views,stored-procedures,policies,triggers} (Rule 6), /src layout.
- Known follow-ups: strip pre-existing SQL comments (Rule 1) from a few SP files; full view-column<->view-entity alignment verified at Phase 2 query time.
2. Rewrite/merge 166 SPs → snake_case + unified (business/user pairs collapsed, `p_users_id`, `tenants_id`); `sp_create_organization`→`sp_create_tenant` (tenant + first admin + magic link). Produce `SP_MIGRATION_MAP.md`.
3. Rename/rewrite 29 views `v_*`→`vw_*`; `v_organizations`→`vw_tenants`, `v_business_users`→`vw_users`, `v_business_user_events`→`vw_user_events`; emit snake_case columns matching view entities.
4. Rewrite `InstallSqlArtifacts` migration for new folder namespaces; regenerate. Run migration green against DB.
5. (Phase 3) Move SQL out of csproj folder to `database-scripts/{views,stored-procedures,policies,triggers}` (Rule 6); EmbeddedResource currently still `Sql/**`.
6. Lookups constants class (api/Constants/Lookups.cs) in Phase 2.

## Deviation notes
- Kept both `vw_users` (from business_users) and `vw_user_profile` as separate views rather than merging (simpler; both map cleanly). Revisit if redundant.
- SQL still under `Db/Sql/**` (violates Rule 6) — folder move deferred to Phase 3 per agreed work order.
