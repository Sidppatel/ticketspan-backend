# Base images pinned by digest to prevent unreviewed image drift / supply-chain
# substitution. Refresh digests with Dependabot/Renovate on a schedule; do not
# bump blindly — validate the rebuilt image before promoting.
#
# Schema migrations are NOT bundled into this image. They run via
# .github/workflows/migrate-prod.yml against the Supabase session pooler
# before the Render deploy. Program.cs verifies __EFMigrationsHistory at boot.

# =========================
# BACKEND BUILD STAGE
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:732cd42c6f659814c9804ad7b05c7f761e83ef8379c5b2fdc3af673353caff73 AS build
RUN apk add --no-cache libc6-compat
WORKDIR /src

# 1. Copy solution + every project file before restoring so the restore
#    layer is only invalidated when dependencies actually change.
COPY ticketspanEventBackEnd.slnx ./
COPY src/Api/Api.csproj                ./src/Api/
COPY src/Contracts/Contracts.csproj    ./src/Contracts/
COPY protos/ ./protos/

# 2. Restore only the publishable API project.
RUN dotnet restore src/Api/Api.csproj

# 3. Copy the rest of the source
COPY . .

# 4. Publish — no restore, no native app host (smaller image, faster publish)
RUN dotnet publish src/Api/Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false

# =========================
# RUNTIME STAGE
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:1201dde897ab436b7c6b386f6dbd4f9a3ca0245f9c5a8aac8f8bcdccb4c7d484 AS runtime

# Pin native dependencies to concrete Alpine package versions so the runtime
# image is reproducible. Refresh these together with the aspnet digest above.
#   krb5-libs       — Kerberos auth (Npgsql GSSAPI fallback path).
#   ca-certificates — Mozilla root CA bundle. Required for TLS handshake
#                     against Upstash Redis (rediss://) + Supabase Postgres
#                     (sslmode=VerifyFull). Without it OpenSSL aborts the
#                     handshake natively → SIGSEGV (exit 139).
#   openssl        — keep in sync with libssl shipped by the base image.
RUN apk add --no-cache \
        krb5-libs=1.22.1-r0 \
        ca-certificates \
        openssl \
 && update-ca-certificates

WORKDIR /app

COPY --from=build    --chown=app:app /app/publish /app
COPY --chown=app:app docker/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

USER app

# Render supplies PORT at runtime (set to 10000 in render.yaml). The Dockerfile
# and Program.cs fallback both use 10000 so local docker-run parity matches prod.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000
EXPOSE 10000

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:${PORT}/health/live || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]
