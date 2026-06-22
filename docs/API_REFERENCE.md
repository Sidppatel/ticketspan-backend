# Svyne Backend API Reference

Transport: **gRPC-Web over HTTPS** (Protobuf). REST is used only for the documented exceptions (Stripe webhooks, file upload, health). Contracts: `protos/` → generated `Svyne.Protos`. Mutations return `svyne.common.AckResponse { success, message, code }`.

Auth: JWT bearer. Claims: `sub` (users_id), `email`, `role` (0 Attendee, 1 Main Admin, 2 Staff, 3 Sub-Tenant, 99 Developer), `tenants_id` (null for developers), `tenant_slug`. Tenant/RLS context (`app.current_user_id`, `app.current_tenant`) is set per request from the JWT.

## gRPC services (`protos/`)

### AuthService (auth.proto)
- `Login(email, password, tenant_slug)` → AuthResponse — verifies bcrypt+versioned-pepper, rehashes on pepper rotation.
- `GoogleSignIn(google_token, tenant_slug)` → AuthResponse — validates Google id-token, links/creates by google_subject.
- `RequestMagicLink` / `VerifyMagicLink` → AuthResponse.
- `RequestPasswordReset` / `SetPassword` → Ack.
- `Logout(session_hash)` → Ack. `Me()` → UserProfile.

### TenantService (tenant.proto) — developer-gated
- `CreateTenant` → tenant + first admin (role 1) + magic-link setup URL.
- `UpdateTenant`, `ArchiveTenant`, `GetTenant`, `ListTenants`, `ListTenantMembers`, `GetTenantStripeStatus`.

### EventService (event.proto)
- `CreateEvent`, `UpdateEvent`, `DeleteEvent`, `GetEvent`, `GetEventBySlug`, `ListEvents`, `SearchEvents`, `ChangeEventStatus`, `GetEventStats`.

### VenueService / PerformerService / SponsorService (catalog.proto)
- Venue: Create/Update/Get/List. Performer & Sponsor: Create/Update/Delete/List + SetEvent{Performers,Sponsors}.

### PurchaseService / TicketService / CheckInService (purchase.proto)
- Purchase: Create, ReserveOpenCapacity, Confirm, Cancel, Refund, Get, List, GetPurchaseStats.
- Ticket: Get, ListPurchaseTickets, Claim, Invite.
- CheckIn: Scan(qr_token), GetCheckInStats.

### TableBookingService (booking.proto)
- ListTablesForEvent, SaveEventLayout, LockTable, ReleaseTableLock, Create/Delete EventTable, Create/Delete EventTicketType.

### admin.proto
- DashboardService: GetAdminDashboard, GetDeveloperDashboard.
- FinancialService: GetMonthlyReport, GetStripeStatus, StartStripeOnboarding.
- StaffService: ListStaffForEvent, AssignStaff, UnassignStaff.
- InvitationService: CreateInvitation, AcceptInvitation, RevokeInvitation, ListInvitations.
- LogService: GetAdminLogs, GetSystemLogs, GetDeveloperLogs.
- FeedbackService: CreateFeedback, ListFeedback, DeleteFeedback.
- HealthService: Check.

## REST endpoints (exceptions)
- `GET /health/live`, `GET /health/ready` — liveness/readiness (Docker healthcheck).
- `POST /webhooks/stripe` — Stripe webhook (Stripe-Signature verified when `STRIPE_WEBHOOK_SECRET` set). Anonymous.
- `POST /uploads/images` (multipart/form-data, auth required) — fields: file, entityType, entityId. 10MB limit, MIME-validated (jpeg/png/webp/gif). Stores bytes to object storage (S3 via `S3_BUCKET`/`S3_SERVICE_URL`, local-disk fallback) then records metadata via `sp_create_image`. Returns `{ imagesId, storageKey }`.

## Config (env)
`DATABASE_URL`, `JWT_SIGNING_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_LIFETIME_MINUTES`, `PASSWORD_PEPPER_V<n>`, `PASSWORD_PEPPER_CURRENT`, `STRIPE_WEBHOOK_SECRET`, `PUBLIC_BASE_URL`, `GRPC_HTTP2_ONLY` (local plaintext native-gRPC testing).

## Run
- API: `dotnet run --project src/Api` (Http1AndHttp2; gRPC-Web + REST).
- Migrations (Rule 15, .NET only): `dotnet run --project database-scripts/MigrationRunner` or `dotnet ef database update --project database-scripts/Db`.
