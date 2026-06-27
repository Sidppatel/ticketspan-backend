using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Email;
using Svyne.Api.Payments;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Tenant;

namespace Svyne.Api.Services;

public sealed class TenantServiceImpl : TenantService.TenantServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly IConfiguration configuration;
    private readonly StripeService stripe;
    private readonly IEmailService email;
    private readonly EmailTemplateRenderer templates;
    private readonly AppSettingsProvider settings;
    private readonly ILogger<TenantServiceImpl> logger;

    public TenantServiceImpl(Db db, TenantContext tenantContext, IConfiguration configuration,
        StripeService stripe, IEmailService email, EmailTemplateRenderer templates,
        AppSettingsProvider settings, ILogger<TenantServiceImpl> logger)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.configuration = configuration;
        this.stripe = stripe;
        this.email = email;
        this.templates = templates;
        this.settings = settings;
        this.logger = logger;
    }

    public override async Task<CreateTenantResponse> CreateTenant(CreateTenantRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var emailHash = EmailHasher.Hash(request.AdminEmail);
        var magicToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var magicHash = EmailHasher.Hash(magicToken);
        var expiryDays = await settings.GetIntAsync("tenant_setup_expiry_days", 7, ct);

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT out_tenants_id, out_users_id FROM sp_create_tenant(@slug, @name, @email, @hash, @first, @last, @mhash, @exp, @legal, @cc)", connection);
        cmd.Parameters.AddWithValue("slug", request.Slug);
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("email", request.AdminEmail);
        cmd.Parameters.AddWithValue("hash", emailHash);
        cmd.Parameters.AddWithValue("first", request.AdminFirstName);
        cmd.Parameters.AddWithValue("last", request.AdminLastName);
        cmd.Parameters.AddWithValue("mhash", magicHash);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddDays(expiryDays));
        cmd.Parameters.AddWithValue("legal", (object?)NullIfEmpty(request.LegalName) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cc", string.IsNullOrEmpty(request.CountryCode) ? "US" : request.CountryCode);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.Internal, "Tenant creation failed"));
        }
        var tenantsId = reader.GetGuid(0);
        var adminUsersId = reader.GetGuid(1);
        await reader.CloseAsync();

        // Persist the Stripe onboarding prefill in the tenant's profile row so it
        // can be edited later (developer-only) and reused when the account is
        // created at onboarding time.
        await using (var prefillCmd = new NpgsqlCommand(
            "INSERT INTO tenant_stripe_profiles "
            + "(tenants_id, business_type, business_url, product_description, mcc, support_email, created_at, updated_at) "
            + "VALUES (@t, @bt, @url, @desc, @mcc, @email, now(), now())", connection))
        {
            prefillCmd.Parameters.AddWithValue("bt", (object?)NullIfEmpty(request.BusinessType) ?? DBNull.Value);
            prefillCmd.Parameters.AddWithValue("url", (object?)NullIfEmpty(request.BusinessUrl) ?? DBNull.Value);
            prefillCmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.ProductDescription) ?? DBNull.Value);
            prefillCmd.Parameters.AddWithValue("mcc", (object?)NullIfEmpty(request.Mcc) ?? DBNull.Value);
            prefillCmd.Parameters.AddWithValue("email", (object?)NullIfEmpty(request.SupportEmail) ?? DBNull.Value);
            prefillCmd.Parameters.AddWithValue("t", tenantsId);
            await prefillCmd.ExecuteNonQueryAsync(ct);
        }

        // Pre-create the Stripe connected account with prefill so the seller's
        // Express onboarding form starts populated. Best-effort: a Stripe failure
        // must not abort tenant creation (account is created lazily otherwise).
        if (stripe.Configured)
        {
            try
            {
                var accountId = await stripe.CreateExpressAccountAsync(new StripeAccountPrefill
                {
                    Country = string.IsNullOrEmpty(request.CountryCode) ? "US" : request.CountryCode,
                    Email = request.AdminEmail,
                    BusinessType = NullIfEmpty(request.BusinessType),
                    BusinessName = NullIfEmpty(request.LegalName) ?? request.Name,
                    Url = NullIfEmpty(request.BusinessUrl),
                    ProductDescription = NullIfEmpty(request.ProductDescription),
                    Mcc = NullIfEmpty(request.Mcc),
                    SupportEmail = NullIfEmpty(request.SupportEmail) ?? request.AdminEmail,
                    IndividualFirstName = NullIfEmpty(request.AdminFirstName),
                    IndividualLastName = NullIfEmpty(request.AdminLastName)
                }, ct);

                await using var acctCmd = new NpgsqlCommand(
                    "SELECT sp_update_tenant_stripe_account(@t, @acct)", connection);
                acctCmd.Parameters.AddWithValue("t", tenantsId);
                acctCmd.Parameters.AddWithValue("acct", accountId);
                await acctCmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to pre-create Stripe account for tenant {Tenant}", tenantsId);
            }
        }

        // Setup opens on the admin portal host. {slug} still supported for back-compat if configured.
        var setupBase = await settings.GetStringAsync("tenant_setup_link_base", "http://admin.localhost:5173/set-password", ct);
        setupBase = string.IsNullOrEmpty(request.Slug)
            ? setupBase.Replace("{slug}.", string.Empty).Replace("{slug}", string.Empty)
            : setupBase.Replace("{slug}", request.Slug);
        var separator = setupBase.Contains('?') ? "&" : "?";
        var setupUrl = $"{setupBase}{separator}token={magicToken}";
        // The admin portal is a shared host (admin.localhost); carry the tenant slug
        // so the portal stores it and login can resolve the correct tenant.
        if (!string.IsNullOrEmpty(request.Slug))
        {
            setupUrl += $"&tenant={Uri.EscapeDataString(request.Slug)}";
        }

        // Local dev writes the email as .html to LOCAL_EMAIL_DIR instead of sending.
        // Best-effort: a delivery failure must not abort tenant creation.
        try
        {
            var fromAddress = await settings.GetStringAsync("tenant_setup_email", "noreply@svyne.com", ct);
            var subject = await settings.GetStringAsync("tenant_setup_subject", "Activate your Svyne workspace", ct);
            var values = new Dictionary<string, string>
            {
                ["Subject"] = subject,
                ["Email"] = request.AdminEmail,
                ["FirstName"] = string.IsNullOrEmpty(request.AdminFirstName) ? "there" : request.AdminFirstName,
                ["TenantName"] = request.Name,
                ["SetupLink"] = setupUrl,
                ["ExpiryDays"] = expiryDays.ToString()
            };
            var htmlBody = await templates.RenderAsync("tenant_admin_setup.html", values, ct);
            await email.SendAsync(fromAddress, request.AdminEmail, subject, htmlBody, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send tenant setup email for {Tenant}", tenantsId);
        }

        return new CreateTenantResponse
        {
            TenantsId = tenantsId.ToString(),
            AdminUsersId = adminUsersId.ToString(),
            SetupUrl = setupUrl
        };
    }

    public override async Task<ListTenantsResponse> ListTenants(PageRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var response = new ListTenantsResponse { Meta = new PageMeta { Offset = request.Offset, Limit = request.Limit } };

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT tenants_id, slug, name, legal_name, country_code, member_count, archived_at IS NOT NULL "
            + "FROM sp_list_tenants(@search, false, @offset, @limit)", connection);
        cmd.Parameters.AddWithValue("search", (object?)NullIfEmpty(request.Search) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("offset", request.Offset);
        cmd.Parameters.AddWithValue("limit", request.Limit <= 0 ? 25 : request.Limit);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Tenants.Add(new Tenant
            {
                TenantsId = reader.GetGuid(0).ToString(),
                Slug = reader.GetString(1),
                Name = reader.GetString(2),
                LegalName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                CountryCode = reader.GetString(4),
                MemberCount = reader.GetInt32(5),
                Archived = reader.GetBoolean(6)
            });
        }
        response.Meta.Total = response.Tenants.Count;
        return response;
    }

    public override async Task<ListPublicTenantsResponse> ListPublicTenants(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListPublicTenantsResponse();
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT slug, name FROM tenants WHERE archived_at IS NULL ORDER BY name", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Tenants.Add(new PublicTenant
            {
                Slug = reader.GetString(0),
                Name = reader.GetString(1)
            });
        }
        return response;
    }

    public override async Task<ListTenantMembersResponse> ListTenantMembers(UuidValue request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var response = new ListTenantMembersResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT users_id, email, role, display_name FROM sp_get_tenant_members(@t)", connection);
        cmd.Parameters.AddWithValue("t", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Members.Add(new TenantMember
            {
                UsersId = reader.GetGuid(0).ToString(),
                Email = reader.GetString(1),
                Role = reader.GetInt16(2),
                DisplayName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
            });
        }
        return response;
    }

    public override async Task<Tenant> GetTenant(UuidValue request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT v.tenants_id, v.slug, v.name, v.legal_name, v.country_code, v.member_count, v.event_count, v.total_revenue_cents, v.archived_at IS NOT NULL, "
            + "(SELECT default_fee_formulas_id FROM tenants t WHERE t.tenants_id = v.tenants_id) "
            + "FROM vw_tenants v WHERE v.tenants_id = @id", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Tenant not found"));
        }
        return new Tenant
        {
            TenantsId = reader.GetGuid(0).ToString(),
            Slug = reader.GetString(1),
            Name = reader.GetString(2),
            LegalName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            CountryCode = reader.GetString(4),
            MemberCount = reader.GetInt32(5),
            EventCount = reader.GetInt32(6),
            TotalRevenueCents = reader.GetInt64(7),
            Archived = reader.GetBoolean(8),
            DefaultFeeFormulasId = reader.IsDBNull(9) ? string.Empty : reader.GetGuid(9).ToString()
        };
    }

    public override async Task<AckResponse> UpdateTenant(UpdateTenantRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_update_tenant(@id, @name, @legal, @cc)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.TenantsId));
        cmd.Parameters.AddWithValue("name", (object?)NullIfEmpty(request.Name) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("legal", (object?)NullIfEmpty(request.LegalName) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cc", (object?)NullIfEmpty(request.CountryCode) ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Tenant updated" };
    }

    public override async Task<AckResponse> ArchiveTenant(UuidValue request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_archive_tenant(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Tenant archived" };
    }

    public override async Task<TenantStripeStatus> GetTenantStripeStatus(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT stripe_connected_account_id, stripe_charges_enabled, stripe_payouts_enabled, stripe_details_submitted "
            + "FROM sp_get_tenant_stripe_status(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Tenant not found"));
        }
        return new TenantStripeStatus
        {
            TenantsId = request.Value,
            StripeConnectedAccountId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            ChargesEnabled = reader.GetBoolean(1),
            PayoutsEnabled = reader.GetBoolean(2),
            DetailsSubmitted = reader.GetBoolean(3)
        };
    }

    public override async Task<TenantStripeProfile> GetTenantStripeProfile(UuidValue request, ServerCallContext context)
    {
        // Developer or the tenant's own admin may view. Only developer may edit
        // (see UpdateTenantStripeProfile) — the data is developer-owned.
        RequireDeveloperOrOwnTenant(request.Value);
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT t.stripe_connected_account_id, COALESCE(t.legal_name, t.name), "
            + "COALESCE(p.business_type, ''), COALESCE(p.business_url, ''), "
            + "COALESCE(p.product_description, ''), COALESCE(p.mcc, ''), COALESCE(p.support_email, '') "
            + "FROM tenants t LEFT JOIN tenant_stripe_profiles p ON p.tenants_id = t.tenants_id "
            + "WHERE t.tenants_id = @t", connection);
        cmd.Parameters.AddWithValue("t", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Tenant not found"));
        }
        return new TenantStripeProfile
        {
            HasAccount = !reader.IsDBNull(0),
            BusinessName = reader.GetString(1),
            BusinessType = reader.GetString(2),
            BusinessUrl = reader.GetString(3),
            ProductDescription = reader.GetString(4),
            Mcc = reader.GetString(5),
            SupportEmail = reader.GetString(6)
        };
    }

    public override async Task<AckResponse> UpdateTenantStripeProfile(UpdateTenantStripeProfileRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        // Keep legal_name in sync with the business name the developer entered,
        // and grab the connected account id (if any) to push edits to Stripe.
        string? accountId;
        await using (var cmd = new NpgsqlCommand(
            "UPDATE tenants SET legal_name = COALESCE(@bizname, legal_name), updated_at = now() "
            + "WHERE tenants_id = @t RETURNING stripe_connected_account_id", connection))
        {
            cmd.Parameters.AddWithValue("bizname", (object?)NullIfEmpty(request.BusinessName) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("t", tenantsId);
            accountId = await cmd.ExecuteScalarAsync(ct) as string;
        }

        // Upsert the developer-owned Stripe profile row.
        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO tenant_stripe_profiles "
            + "(tenants_id, business_type, business_url, product_description, mcc, support_email, created_at, updated_at) "
            + "VALUES (@t, @bt, @url, @desc, @mcc, @email, now(), now()) "
            + "ON CONFLICT (tenants_id) DO UPDATE SET "
            + "business_type = EXCLUDED.business_type, business_url = EXCLUDED.business_url, "
            + "product_description = EXCLUDED.product_description, mcc = EXCLUDED.mcc, "
            + "support_email = EXCLUDED.support_email, updated_at = now()", connection))
        {
            cmd.Parameters.AddWithValue("bt", (object?)NullIfEmpty(request.BusinessType) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("url", (object?)NullIfEmpty(request.BusinessUrl) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.ProductDescription) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("mcc", (object?)NullIfEmpty(request.Mcc) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("email", (object?)NullIfEmpty(request.SupportEmail) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("t", tenantsId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // If the connected account already exists, push the edits to Stripe too.
        if (stripe.Configured && !string.IsNullOrEmpty(accountId))
        {
            try
            {
                await stripe.UpdateAccountAsync(accountId, BuildPrefill(request), ct);
            }
            catch (Stripe.StripeException ex)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, ex.StripeError?.Message ?? ex.Message));
            }
        }
        return new AckResponse { Success = true, Message = "Stripe profile saved" };
    }

    private static StripeAccountPrefill BuildPrefill(UpdateTenantStripeProfileRequest r) => new()
    {
        BusinessType = NullIfEmpty(r.BusinessType),
        BusinessName = NullIfEmpty(r.BusinessName),
        Url = NullIfEmpty(r.BusinessUrl),
        ProductDescription = NullIfEmpty(r.ProductDescription),
        Mcc = NullIfEmpty(r.Mcc),
        SupportEmail = NullIfEmpty(r.SupportEmail)
    };

    private void RequireDeveloper()
    {
        if (!tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Developer access required"));
        }
    }

    // Read-only access: a developer (any tenant) or a member of the requested tenant.
    private void RequireDeveloperOrOwnTenant(string tenantsId)
    {
        if (tenantContext.IsDeveloper)
        {
            return;
        }
        if (tenantContext.TenantsId is { } own && Guid.TryParse(tenantsId, out var requested) && own == requested)
        {
            return;
        }
        throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
