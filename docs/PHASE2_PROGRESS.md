# Phase 2 Progress (gRPC)

## Done + verified
- **protos/** — single source of truth, all 20 services across 8 files (common, auth, tenant, event, catalog[venue/performer/sponsor], purchase[purchase/ticket/checkin], booking, admin[dashboard/financial/staff/invitation/log/feedback/health]). AckResponse on mutations (RULES Comm §ACK). Zero comments (Rule 1).
- **src/Contracts** — Grpc.Tools codegen from protos/ → `Svyne.Protos.dll`. Builds 0/0. Stub source for frontend too.
- **src/Api** — gRPC-Web host (the Phase 3 /src target). Builds 0/0 with TreatWarningsAsErrors.
  - `PasswordHasher`: bcrypt + versioned HMAC pepper (`PASSWORD_PEPPER_V<n>` env, `PASSWORD_PEPPER_CURRENT`), rehash-on-login, per-row `pepper_version`.
  - `JwtTokenService`: HS256, claims sub/email/role/tenants_id/tenant_slug.
  - `EmailHasher`: SHA256 deterministic email_hash.
  - `Db`: Npgsql connection, sets `app.current_user_id`/`app.current_tenant` per request (RLS context).
  - `TenantResolutionMiddleware`: reads JWT claims → TenantContext; rejects non-dev with null tenant.
  - Services implemented (6): **AuthService** (Login w/ pepper verify+rehash, GoogleSignIn validate+link, Me), **TenantService** (CreateTenant, ListTenants, ListTenantMembers — developer-gated), **EventService** (CreateEvent, ChangeEventStatus, SearchEvents, GetEventStats), **VenueService** (Create/Update/Get/List), **PerformerService** (Create/Update/Delete/List/SetEventPerformers), **SponsorService** (Create/Update/Delete/List/SetEventSponsors).
  - `Program.cs`: Kestrel h2c (Http2), JWT bearer auth (inbound claim map cleared), gRPC-Web, middleware, 6 services mapped.
- **tests/GrpcSmoke** — real gRPC client e2e against live DB. Verified: dev-JWT CreateTenant/ListTenants/ListTenantMembers (tenant+admin+magic-link), admin-JWT CreateVenue/ListVenues/CreatePerformer (tenant-context path + RLS). All operations succeed e2e (gRPC → SP → DB).

## Also implemented (build 0/0; PurchaseService/CheckInService via verified SPs, gRPC pattern identical to e2e-verified services)
- **PurchaseService**: CreatePurchase, ReserveOpenCapacity, ConfirmPurchase, CancelPurchase, RefundPurchase, GetPurchaseStats.
- **CheckInService**: Scan (sp_check_in_ticket), GetCheckInStats.
- Total 8 gRPC services mapped in Program.cs.

## Remaining (same pattern, mechanical)
- TicketService, TableBookingService, DashboardService, FinancialService, StaffService, InvitationService, LogService, FeedbackService, HealthService, plus rest of Auth (magic link, password reset, refresh, logout) and Event (Update/Delete/Get/List) and Purchase (Get/List). Each maps gRPC → existing verified SPs (writes via `SELECT sp_*`) / vw_* (reads via `SELECT ... FROM vw_*`), using `Db.OpenAsync(usersId, tenantsId)` for RLS context. Mutations return AckResponse.
- WebhooksController (Stripe) + image multipart upload stay HTTPS (RULES exceptions) — port as minimal REST endpoints.
- Retire legacy `api/` project (35 controllers, 68 services, 62 repos — references deleted entities, does not compile). Replace with src/Api services.
- Local-dev JSON transcoding under #if DEBUG (RULES Comm §Local Dev).

## Phase 3 remaining
- Move SQL from `database-scripts/Db/Sql/**` to `database-scripts/{views,stored-procedures,policies,triggers,init-data,seed-data}` (Rule 6/14); update InstallSqlArtifacts embedded-resource roots + EmbeddedResource glob.
- Move `database-scripts/Db` (DbContext/entities/migrations) into `/src` (keep .csproj SQL-free).
- Update docs/API_REFERENCE.md.

## Run
Host: `cd src/Api; DATABASE_URL=... PASSWORD_PEPPER_V1=... JWT_SIGNING_KEY=... dotnet run` (Http2/h2c on chosen port).
Smoke: `cd tests/GrpcSmoke; dotnet run` (expects host on :5599, DB svyne_fresh).

## COMPLETION UPDATE
- 17 gRPC services implemented + mapped (Auth, Tenant, Event, Venue, Performer, Sponsor, Purchase, Ticket, CheckIn, TableBooking, Dashboard, Staff, Invitation, Feedback, Log, Financial, Health). Solution builds 0 warnings / 0 errors.
- REST exceptions added (src/Api/Program.cs): /health/live, /health/ready, POST /webhooks/stripe (signature-verified), POST /uploads/images (multipart, auth, MIME+size validated, metadata via sp_create_image).
- Kestrel Http1AndHttp2 (gRPC-Web + REST); GRPC_HTTP2_ONLY env for local native-gRPC testing.
- Legacy retired: deleted api/, contracts/, tests/{Api.Tests,IntegrationTests,SchemaTests}, database-scripts/SchemaSnapshot. Solution rewired to src/Contracts, src/Api, database-scripts/{Db,MigrationRunner}, tests/GrpcSmoke. Dockerfile + Dockerfile.migrate updated.
- Phase 3: SQL moved out of csproj folder to database-scripts/sql (Rule 6: 0 .sql in Db csproj folder), linked-embed preserves resource names. From-scratch `dotnet ef database update` verified (31 views, 25 policies).
- E2E verified on migration-built DB: CreateTenant/CreateEvent/GetEvent/DeveloperDashboard/Health/Venue/Performer over gRPC (dev + admin JWT) = SMOKE PASS.

## Gaps CLOSED
- Image upload: real blob storage via `ObjectStorage` (AWSSDK.S3 with `S3_BUCKET`/`S3_SERVICE_URL`; local-disk fallback for dev). Endpoint writes bytes then records metadata via sp_create_image.
- ALL proto RPCs implemented — every declared rpc across the 8 protos has an override (verified: rpc set == implemented set). Added Tenant Get/Update/Archive/StripeStatus, TableBooking GetEventLayout, Auth RefreshToken.
- SQL reorganized into RULES folders: `database-scripts/sql/{functions,views,stored-procedures,policies}` (one file per object). From-scratch `dotnet ef database update` verified: 166 SPs, 31 views, 25 policies, 3 app fns. View load order preserved via numeric prefixes (single-pass loader). Loader namespace for hyphen folder uses `Sql.stored_procedures` (MSBuild munges `-`→`_` in resource names).
- Db csproj folder contains 0 .sql (Rule 6).
