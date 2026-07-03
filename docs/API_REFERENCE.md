# Svyne Backend API Reference

Transport: **gRPC-Web over HTTPS** (Protobuf). REST is used only for the documented exceptions (Stripe webhooks, file upload, health). Contracts: `protos/` → generated `Svyne.Protos`. Mutations return `svyne.common.AckResponse { success, message, code }`.

Auth: JWT bearer. Claims: `sub` (users_id), `email`, `role` (0 Attendee, 1 Main Admin, 2 Staff, 3 Sub-Tenant, 99 Developer), `tenants_id` (null for developers), `tenant_slug`. Tenant/RLS context (`app.current_user_id`, `app.current_tenant`) is set per request from the JWT.

Client setup (React/Next.js/mobile, codegen, auth interceptor): see [FRONTEND_INTEGRATION.md](FRONTEND_INTEGRATION.md).

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

### TenantService — tenant admin + public branding

- `GetMyTenant()` → Tenant (includes `logo_url` and the seven `brand_*` colors: primary, secondary, accent, background, text, button, highlight).
- `UpdateMyTenantBranding(logo_images_id, brand_primary, brand_secondary, brand_accent, brand_background, brand_text, brand_button, brand_highlight)` → Ack. Calls `sp_update_tenant_branding`; empty color = cleared (falls back to platform defaults on the frontend).
- `GetPublicTenantBranding(slug)` → PublicTenantBranding (name, logo_url, seven `brand_*` colors). Anonymous; used by the public portal to theme `events.{tenant}` pages via CSS variables. Reads `sp_get_public_tenant_branding`.

### EventService (event.proto)

- `CreateEvent`, `UpdateEvent`, `DeleteEvent`, `GetEvent`, `GetEventBySlug`, `ListEvents`, `SearchEvents`, `ChangeEventStatus`, `GetEventStats`, `SetEventFeesIncluded`.
- Schedule timeline: `ListScheduleItems(UuidValue events_id)` → `ListScheduleItemsResponse`; `CreateScheduleItem(events_id, title, type_category, start_time, end_time)` → `UuidValue`; `UpdateScheduleItem(schedule_items_id, title, type_category, start_time, end_time)` → `AckResponse` (empty/0 fields = unchanged); `DeleteScheduleItem(UuidValue)` → `AckResponse`. Times are int64 unix seconds. Constraints (enforced in `sp_*`, returned as `FailedPrecondition`): `end_time > start_time`, item within the event's `[start_date, end_date]` window, no overlap with sibling items; `type_category` ∈ {Performance, Break, Intermission, DJ Set, Networking, Other}. Items are always returned ordered by `start_time`.

### VenueService / PerformerService / SponsorService (catalog.proto)

- Venue: Create/Update/Get/List. Performer & Sponsor: Create/Update/Delete/List + SetEvent{Performers,Sponsors}.

### BookingService / TicketService / CheckInService (bookings.proto)

- Booking: Create, ReserveOpenCapacity, Confirm, Cancel, Refund, Get, List, GetBookingStats.
- Ticket: Get, ListTickets, Claim, Invite.
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

### ReportingService (reporting.proto) — tenant admin

All revenue figures are the organizer's own prices (`subtotal_cents` / `selling_price_cents`); platform and gateway fees are never returned. Ranges are int64 unix seconds. Advanced access = tier ∈ {professional, business, enterprise} OR the developer override, checked server-side per call via `vw_tenant_reporting_access` with a 30s in-memory cache (invalidated on tier/override change).

- `GetReportingAccess()` → ReportingAccess (tier, has_advanced_reporting, advanced_reporting_override) — drives UI gating.
- `GetReportSummary(from, to)` → revenue, orders, tickets, average order value, visits (PageView audit rows), conversion bps; refund fields zeroed for basic tiers. Reads `sp_report_summary`.
- `GetRevenueTimeseries(from, to, bucket)` → per-bucket revenue/orders/tickets; bucket ∈ {day, week, month}; `year` is advanced-only (`PermissionDenied` otherwise). Reads `sp_report_revenue_timeseries`.
- `GetEventPerformance(from, to)` → per-event revenue, tickets, checked-in, attendance bps; capacity, velocity, revenue-per-attendee, and refunds zeroed for basic tiers. Reads `sp_report_event_performance`.
- `GetTicketTypeBreakdown(from, to)` → per ticket type: price, quantity sold, revenue (+ refunds, advanced-only). Reads `sp_report_ticket_type_breakdown`.
- `GetSalesByChannel(from, to)` → orders/tickets/revenue by `bookings.sales_channel` — advanced-only; grant/deny attempts audited as `ReportingAccess` events in `audit_logs`. Reads `sp_report_sales_by_channel`.

### TenantTierService (reporting.proto) — developer-gated

- `ListTenantReportingAccess(PageRequest)` → all tenants with tier, override flag, and effective advanced access (search on name/slug; from `vw_tenant_reporting_access`).
- `SetTenantTier(tenants_id, tier)` → Ack — tier ∈ {free, starter, professional, business, enterprise}; audited (`TenantTier` / `tier_changed` with from/to); takes effect immediately (access cache invalidated).
- `SetTenantAdvancedReporting(tenants_id, enabled)` → Ack — developer override; audited (`advanced_reporting_toggled`); immediate.

## REST endpoints (exceptions)

- `GET /health/live`, `GET /health/ready` — liveness/readiness (Docker healthcheck).
- `POST /webhooks/stripe` — Stripe webhook (Stripe-Signature verified when `STRIPE_WEBHOOK_SECRET` set). Anonymous.
- `POST /uploads/images` (multipart/form-data, auth required) — fields: file, entityType, entityId. 10MB limit, MIME-validated (jpeg/png/webp/gif). Stores bytes to object storage (S3 via `S3_BUCKET`/`S3_SERVICE_URL`, local-disk fallback) then records metadata via `sp_create_image`. Returns `{ imagesId, storageKey }`.

## Config (env)

`DATABASE_URL`, `JWT_SIGNING_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_LIFETIME_MINUTES`, `PASSWORD_PEPPER_V<n>`, `PASSWORD_PEPPER_CURRENT`, `STRIPE_WEBHOOK_SECRET`, `PUBLIC_BASE_URL`, `GRPC_HTTP2_ONLY` (local plaintext native-gRPC testing).

## Run

- API: `dotnet run --project src/Api` (Http1AndHttp2; gRPC-Web + REST).
- Migrations (Rule 15, .NET only): `dotnet run --project database-scripts/MigrationRunner` or `dotnet ef database update --project database-scripts/Db`.
