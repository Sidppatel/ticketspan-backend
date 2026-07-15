# TicketSpan Backend API Reference

Transport: **gRPC-Web over HTTPS** (Protobuf). REST is used only for the documented exceptions (Stripe webhooks, file upload, health). Contracts: `protos/` → generated `TicketSpan.Protos`. Mutations return `ticketspan.common.AckResponse { success, message, code }`.

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
- CheckIn: Scan(qr_token), GetCheckInStats, ListEventsForStaff, GetGuestList, CheckInGuest, `ListCheckInLogs(events_id, staff_user_id?, method?, status?, page, page_size)` → paged audit entries (staff name, attendee, booking #, ticket code/type, timestamp, method ∈ {qr_scan, manual_entry}, status ∈ {success, failed}, failure_reason ∈ {invalid_ticket, wrong_event, already_checked_in, booking_not_paid, ticket_not_claimed, booking_not_found}). Staff limited to assigned events within the ±24h window; admins/developers unrestricted. Every check-in attempt (success and failure) is persisted to `checkin_logs` with method/status/failure_reason; reads via `vw_checkin_logs`.
- `ListEventTicketTypes` items include `sold_count` (Pending/Paid/CheckedIn seats) so admin UIs can render sale locks.

### TableBookingService (booking.proto)

- ListTablesForEvent, SaveEventLayout, LockTable, ReleaseTableLock, Create/Delete EventTable, Create/Delete EventTicketType.
- **Sale locking** (enforced in `sp_*`, surfaced as `FailedPrecondition` with a human-readable message): once a ticket type has Pending/Paid/CheckedIn seats its label, price, and fee formula are immutable and it cannot be deleted; capacity/quantity cannot drop below sold. Once a table is Booked (or actively Locked) it cannot be deleted or moved to another table type, and its table type's price/fee cannot change nor capacity shrink; `SaveEventLayout` silently preserves sold/held tables (position-only updates). `ChangeEventStatus` refuses reverting an event with sales to Draft; `DeleteEvent` refuses when active orders exist (mark the event Cancelled instead). Metadata (description, sort order, colors, positions) stays editable.

### admin.proto

- DashboardService: GetAdminDashboard, GetDeveloperDashboard.
- FinancialService: GetMonthlyReport, GetStripeStatus, StartStripeOnboarding.
- StaffService: ListStaffForEvent, AssignStaff, UnassignStaff.
- InvitationService: CreateInvitation, AcceptInvitation, RevokeInvitation, ListInvitations.
- LogService: GetAdminLogs, GetSystemLogs, GetDeveloperLogs.
- LogService (error logging): `GetErrorLogs(ErrorLogQuery)` → filtered/paged error entries (severity, source backend|frontend, resolved, search on message/path/error-id/correlation-id) — developer-only, reads `sp_get_error_logs`/`sp_count_error_logs`; `GetErrorLogStats()` → today/7d/30d totals, unresolved count, by-severity/daily/top-types/top-tenants breakdowns — developer-only, reads `sp_get_error_log_stats`; `ResolveErrorLog(error_log_id, notes)` → Ack — developer-only, writes resolution into `audit_logs.metadata_json` via `sp_resolve_error_log`; `ReportClientErrors(ClientErrorBatch)` → Ack — anonymous frontend error intake (max 20/batch, 60/IP/minute, client severity capped at High), persisted through the central `ErrorLogger`.
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

### Sales tax (SalesTaxZip)

Tax is computed on the taxable amount (`selling_price + platform_fee + gateway_fee`) using the venue zip's combined rate, and folded into `fee_cents` so `total_cents = subtotal_cents + fee_cents` holds. `SalesTaxService` (C#) resolves the event's venue zip, refreshes `tax_rate_cache` from `GET {SALESTAXZIP_BASE_URL}/api/v1/rate/{zip}` when the cached row is older than `SALESTAXZIP_CACHE_TTL_HOURS` (default 24), then SQL pricing reads the cache. API failures/timeouts/429 fall back to the last cached rate (never blocks checkout); 404 caches a 0% rate. Per-booking tax detail is stored in `booking_taxes`; `bookings` carries `tax_cents`/`tax_rate`/`tax_state|county|city`. Events may set `tax_exempt` or `tax_rate_override` (honored by `app.event_tax_rate`). Config keys: `SALESTAXZIP_BASE_URL` (defaults to `https://salestaxzip.com`), `SALESTAXZIP_CACHE_TTL_HOURS` (all optional). Buyer breakdown exposes `service_fee_cents` + `tax_cents` on `Booking`/`EventTicketType`; Stripe PaymentIntent metadata includes venue + tax fields.

- `DeveloperBillingService.GetTaxReport(from, to)` → TaxReport (developer-gated): total tax collected plus breakdowns by event / tenant / month / jurisdiction (state·county·city + combined rate) / rate summary. Reads `sp_developer_tax_by_*`. Frontend: developer `/tax` page (CSV export).

### TenantTierService (reporting.proto) — developer-gated

- `ListTenantReportingAccess(PageRequest)` → all tenants with tier, override flag, and effective advanced access (search on name/slug; from `vw_tenant_reporting_access`).
- `SetTenantTier(tenants_id, tier)` → Ack — tier ∈ {free, starter, professional, business, enterprise}; audited (`TenantTier` / `tier_changed` with from/to); takes effect immediately (access cache invalidated).
- `SetTenantAdvancedReporting(tenants_id, enabled)` → Ack — developer override; audited (`advanced_reporting_toggled`); immediate.

### Fee overrides (fees.proto / pricing.proto) — audited

- `FeeService.AssignFeeFormula(kind, target_id, fee_formulas_id, reason)` → Ack — attaches/clears a formula on a ticket type or table type. `reason` required for developers; every change writes an `audit_logs` row (`FeeOverride` / `fee_formula_assigned` with kind, from, to, reason). Non-developers are blocked from changing the fee on items with sales (same sale-locking rule).
- `PricingService.SetTenantDefaultFeeFormula(tenants_id, fee_formulas_id, reason)` → Ack — developer-only tenant-level override; `reason` required; audited (`FeeOverride` / `tenant_default_fee_changed` with from/to/reason).

## REST endpoints (exceptions)

- `GET /health/live`, `GET /health/ready` — liveness/readiness (Docker healthcheck).
- `POST /webhooks/stripe` — Stripe webhook (Stripe-Signature verified when `STRIPE_WEBHOOK_SECRET` set). Anonymous.
- `POST /uploads/images` (multipart/form-data, auth required) — fields: file, entityType, entityId. 10MB limit, MIME-validated (jpeg/png/webp/gif). Stores bytes to object storage (S3 via `S3_BUCKET`/`S3_SERVICE_URL`, local-disk fallback) then records metadata via `sp_create_image`. Returns `{ imagesId, storageKey }`.

## Config (env)

`DATABASE_URL`, `JWT_SIGNING_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_LIFETIME_MINUTES`, `PASSWORD_PEPPER_V<n>`, `PASSWORD_PEPPER_CURRENT`, `STRIPE_WEBHOOK_SECRET`, `PUBLIC_BASE_URL`, `GRPC_HTTP2_ONLY` (local plaintext native-gRPC testing).

Error logging/alerting (all optional): `ERROR_LOGGING_DISABLED=true` turns persistence off; `ERROR_ALERTS_SLACK_WEBHOOK_URL` enables Slack notifications for Critical/High errors; `ERROR_ALERTS_EMAIL_TO` (+ `ERROR_ALERTS_EMAIL_FROM`) enables email notifications for Critical errors. Unset in development = log to DB only, no notifications.

## Error handling architecture

- Central class: `src/Api/ErrorHandling/ErrorLogger.cs` — `LogErrorAsync(severity, type, message, exception?, ErrorContext?)` / `LogWarningAsync` / `LogInfoAsync`, all returning the error id. Severities: Critical, High, Medium, Low, Warning, Info. Persists into the unified `audit_logs` table (actor_type `System`, event_type `Exception|Warning|Info`, full context in `metadata_json`) via `sp_log_system_error` on the bootstrap connection so RLS can never block error capture. Never throws — falls back to `ILogger` if persistence fails.
- Automatic capture: `ErrorLoggingInterceptor` wraps every gRPC unary call (unexpected exceptions logged as High, rethrown as `Internal` with an error reference); `ErrorLoggingMiddleware` wraps the REST endpoints (uploads, images, redirects) and returns 500 with the error reference; Stripe webhook failures log as Critical with the Stripe event id; `HoldExpiryWorker` sweep failures log as Medium.
- Correlation: `TenantContext.CorrelationId` (new Guid per request) is stamped on every logged error.
- Frontend intake: `src/shared/errorReporter.ts` captures window errors, unhandled promise rejections, console.error calls, React render errors (`ErrorBoundary`), and failed RPCs (INTERNAL/UNKNOWN/UNAVAILABLE/DEADLINE_EXCEEDED via `callRpc`); batches with session-level dedupe (max 30/session), queues to localStorage when offline, and ships to `LogService.ReportClientErrors` with page/previous URL, screen/viewport size, session id, and last-10-click breadcrumbs.
- Dashboard: developer portal `/logs` — totals, severity/type/tenant breakdowns, filterable + searchable list, per-error detail (stack trace, request/user/business context, correlation id), resolve-with-notes.
- Retention: `sp_cleanup_old_logs` (existing) covers `audit_logs` rows.

## Run

- API: `dotnet run --project src/Api` (Http1AndHttp2; gRPC-Web + REST).
- Migrations (Rule 15, .NET only): `dotnet run --project database-scripts/MigrationRunner` or `dotnet ef database update --project database-scripts/Db`.
