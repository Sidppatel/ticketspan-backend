# TicketSpan Event Backend

The server-side backend engine for **TicketSpan** — a premium multi-tenant event ticketing and table booking platform designed for nightlife, concerts, and exclusive social events.

Built on **.NET 10** using a high-performance **gRPC-Web** interface, raw **PostgreSQL** queries, and server-authoritative money math.

---

## Technical Stack

| Category | Technology | Description |
| --- | --- | --- |
| **Runtime & Language** | .NET 10 (C#) | Latest performance-oriented runtime |
| **API Transport** | gRPC / gRPC-Web (Protobuf) | Strongly-typed, contract-first communication |
| **Database** | PostgreSQL | RDBMS using raw `Npgsql` for custom SPs/Views and fine-grained RLS |
| **Payments** | Stripe.net | Server-authoritative checkout and billing |
| **Storage** | AWS SDK (S3) | Object storage for logos and venue images (local disk fallback) |
| **Auth** | JWT + bcrypt | Decoupled token validation, Google OAuth, and secure password hashing |
| **Email** | Resend API / Local | Email template rendering for bookings and magic links |

---

## Directory Structure

```text
ticketspan-event-backend/
├── database-scripts/        # Database migrations, functions, schema definitions, and seeding
│   ├── Db/                  # Context setup scripts
│   ├── MigrationRunner/     # Custom CLI runner to apply DB migrations in sequence
│   └── sql/                 # Schema changes, stored procedures (sp_*), and views (vw_*)
├── docs/                    # Architectural guidelines and detailed references
│   └── API_REFERENCE.md     # In-depth gRPC definitions & endpoints
├── protos/                  # Protocol Buffer contract files (.proto) for client-server sync
├── src/
│   ├── Api/                 # ASP.NET Core gRPC Host
│   │   ├── Data/            # DB client, RLS context, and connection management
│   │   ├── Email/           # Templates and delivery pipelines (Resend/Local file fallback)
│   │   ├── ErrorHandling/   # Interceptors and central error loggers
│   │   ├── Payments/        # Stripe payment intents, tax computation, and hold workers
│   │   ├── Security/        # Authorization rules and tenant validation interceptors
│   │   ├── Services/        # gRPC Service implementations
│   │   ├── Storage/         # S3 Object Storage adapter
│   │   ├── Program.cs       # Application configuration and DI registry
│   │   └── Api.csproj
│   └── Contracts/           # Common C# DTOs, Enums, and Shared Contracts
├── tests/                   # xUnit tests for endpoint validation and core rules
├── Dockerfile               # Multi-stage production container build
├── Dockerfile.migrate       # Container runner for applying DB schema migrations
├── startup.ps1              # Local PowerShell boot script loading env vars
└── ticketspanEventBackEnd.slnx   # Modern XML-based Visual Studio Solution file
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL 16+](https://www.postgresql.org/download/) (or running via local docker-compose from the parent workspace)
- A tool like [Buf](https://buf.build/) or standard Protobuf tools if modifying protos.

### 1. Setup Local Environment

Copy the example environment file and update it with your database credentials:

```bash
cp .env.example .env
```

Key environment variables to configure:

- `DATABASE_URL`: PostgreSQL connection string (e.g. `Host=localhost;Database=ticketspan_dev;Username=postgres;Password=...`)
- `JWT_SIGNING_KEY`: Custom secure string used to sign auth tokens.
- `STRIPE_SECRET_KEY` & `STRIPE_WEBHOOK_SECRET`: Keys for Stripe payment processing.
- `RESEND_API_KEY`: API token for email delivery (leaves empty to write emails to local files).

### 2. Apply Database Migrations

Migrations are applied sequentially using the custom migration runner. Navigate to the database project or use the runner tool:

```bash
dotnet run --project database-scripts/MigrationRunner/MigrationRunner.csproj
```

### 3. Spin Up the API

Use the local powershell helper script which loads the `.env` configuration file automatically and launches the API:

```powershell
./startup.ps1
```

Alternatively, if not using PowerShell:

```bash
# Export the environment variables manually, then run:
dotnet run --project src/Api/Api.csproj --no-launch-profile
```

---

## API & Services Overview

The backend uses **gRPC-Web** over HTTPS as its primary communications transport. Below is an overview of the core services:

### 🔒 [AuthService](file:///d:/ticketspan-event-system/ticketspan-event-backend/docs/API_REFERENCE.md#L11-L17)

Handles user credential validation, JWT issuance, Google OAuth integration, session management, and magic-link delivery.

### 🏢 [TenantService](file:///d:/ticketspan-event-system/ticketspan-event-backend/docs/API_REFERENCE.md#L19-L29)

Provides multi-tenant separation. Organizers manage branding assets (logo, custom CSS color variables primary/secondary/accent/etc.) retrieved dynamically by the attendee client portal.

### 📅 [EventService](file:///d:/ticketspan-event-system/ticketspan-event-backend/docs/API_REFERENCE.md#L30-L33)

Manages event lifecycle (Draft, Published, Cancelled), scheduling, and timelines with strict conflict/overlap validation rules.

### 🎫 [Booking & TableBookingService](file:///d:/ticketspan-event-system/ticketspan-event-backend/docs/API_REFERENCE.md#L39-L50)

Server-authoritative capacity holds, pricing, tax computation (using zip-code lookup caches), table layouts, and Stripe PaymentIntent checkouts. Implements strict sale locks to prevent updating ticket pricing or deleting tables with active purchases.

### 📊 [Reporting & AdminService](file:///d:/ticketspan-event-system/ticketspan-event-backend/docs/API_REFERENCE.md#L51-L89)

Provides event telemetry, ticket sales breakdown, tax reporting, audit logs, and developer configuration dashboards. Access control logic enforces tiered features (e.g. Starter vs. Professional/Enterprise reporting).

### 🌐 REST Endpoints (Exceptions)

- `GET /health/live` & `/health/ready` - K8s/Docker health checks.
- `POST /webhooks/stripe` - Verifies signatures and processes payment confirmations.
- `POST /uploads/images` - Authenticated multipart image file upload endpoint (saves to AWS S3 or local directory).

---

## Testing

Run the test suite using standard dotnet CLI tools:

```bash
dotnet test
```
