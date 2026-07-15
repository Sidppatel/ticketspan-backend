using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;
using TicketSpan.Api.Payments;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Tenant;

namespace TicketSpan.Api.Services;

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

        await using (var prefillCmd = new NpgsqlCommand(
            "SELECT sp_upsert_tenant_stripe_profile(@t, @bt, @url, @desc, @mcc, @email)", connection))
        {
            prefillCmd.Parameters.AddWithValue("bt", (object?)NullIfEmpty(request.BusinessType) ?? DBNull.Value);
            prefillCmd.Parameters.AddWithValue("url", (object?)NullIfEmpty(request.BusinessUrl) ?? DBNull.Value);
            prefillCmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.ProductDescription) ?? DBNull.Value);
            prefillCmd.Parameters.AddWithValue("mcc", (object?)NullIfEmpty(request.Mcc) ?? DBNull.Value);
            prefillCmd.Parameters.AddWithValue("email", (object?)NullIfEmpty(request.SupportEmail) ?? DBNull.Value);
            prefillCmd.Parameters.AddWithValue("t", tenantsId);
            await prefillCmd.ExecuteNonQueryAsync(ct);
        }

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

        var setupBase = await settings.GetStringAsync("tenant_setup_link_base", "http://admin.localhost:5173/set-password", ct);
        setupBase = string.IsNullOrEmpty(request.Slug)
            ? setupBase.Replace("{slug}.", string.Empty).Replace("{slug}", string.Empty)
            : setupBase.Replace("{slug}", request.Slug);
        var separator = setupBase.Contains('?') ? "&" : "?";
        var setupUrl = $"{setupBase}{separator}token={magicToken}";
        if (!string.IsNullOrEmpty(request.Slug))
        {
            setupUrl += $"&tenant={Uri.EscapeDataString(request.Slug)}";
        }

        try
        {
            var fromAddress = await settings.GetStringAsync("tenant_setup_email", "noreply@ticketspan.com", ct);
            var subject = await settings.GetStringAsync("tenant_setup_subject", "Activate your TicketSpan workspace", ct);
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
            "SELECT v.tenants_id, v.slug, v.name, v.legal_name, v.country_code, v.member_count, v.event_count, v.total_revenue_cents, v.archived_at IS NOT NULL, "
            + "v.ach_enabled, v.default_fee_formulas_id, v.ach_fee_formulas_id "
            + "FROM vw_tenants v "
            + "WHERE v.archived_at IS NULL "
            + "AND (@search::text IS NULL OR v.name ILIKE '%' || @search || '%' OR v.legal_name ILIKE '%' || @search || '%' OR v.slug ILIKE '%' || @search || '%') "
            + "ORDER BY v.created_at DESC OFFSET @offset LIMIT @limit", connection);
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
                EventCount = reader.GetInt32(6),
                TotalRevenueCents = reader.GetInt64(7),
                Archived = reader.GetBoolean(8),
                AchEnabled = reader.GetBoolean(9),
                DefaultFeeFormulasId = reader.IsDBNull(10) ? string.Empty : reader.GetGuid(10).ToString(),
                AchFeeFormulasId = reader.IsDBNull(11) ? string.Empty : reader.GetGuid(11).ToString()
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
            "SELECT slug, name FROM sp_public_tenant_identity() WHERE archived_at IS NULL ORDER BY name", connection);
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
            + "v.default_fee_formulas_id "
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

    public override async Task<Tenant> GetMyTenant(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (tenantContext.UsersId is not { } usersId || tenantContext.TenantsId is null)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
        await using var connection = await db.OpenAsync(usersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT tenants_id, slug, name, legal_name, country_code, "
            + "COALESCE(phone, ''), COALESCE(address_line1, ''), COALESCE(address_line2, ''), "
            + "COALESCE(city, ''), COALESCE(state, ''), COALESCE(zip, ''), "
            + "logo_images_id, COALESCE(brand_primary, ''), COALESCE(brand_secondary, ''), COALESCE(brand_accent, ''), "
            + "COALESCE(brand_background, ''), COALESCE(brand_text, ''), COALESCE(brand_button, ''), COALESCE(brand_highlight, ''), "
            + "COALESCE(brand_tokens::text, '') "
            + "FROM sp_get_my_tenant(@u)", connection);
        cmd.Parameters.AddWithValue("u", usersId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Tenant not found"));
        }
        var logoImagesId = reader.IsDBNull(11) ? (Guid?)null : reader.GetGuid(11);
        var baseUrl = configuration["PUBLIC_BASE_URL"] ?? string.Empty;
        return new Tenant
        {
            TenantsId = reader.GetGuid(0).ToString(),
            Slug = reader.GetString(1),
            Name = reader.GetString(2),
            LegalName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            CountryCode = reader.GetString(4),
            Phone = reader.GetString(5),
            AddressLine1 = reader.GetString(6),
            AddressLine2 = reader.GetString(7),
            City = reader.GetString(8),
            State = reader.GetString(9),
            Zip = reader.GetString(10),
            LogoUrl = logoImagesId is { } logo ? $"{baseUrl}/images/{logo}" : string.Empty,
            BrandPrimary = reader.GetString(12),
            BrandSecondary = reader.GetString(13),
            BrandAccent = reader.GetString(14),
            BrandBackground = reader.GetString(15),
            BrandText = reader.GetString(16),
            BrandButton = reader.GetString(17),
            BrandHighlight = reader.GetString(18),
            BrandTokensJson = reader.GetString(19)
        };
    }

    public override async Task<PublicTenantBranding> GetPublicTenantBranding(PublicTenantBrandingRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Tenant slug required"));
        }
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT slug, name, logo_images_id, "
            + "COALESCE(brand_primary, ''), COALESCE(brand_secondary, ''), COALESCE(brand_accent, ''), "
            + "COALESCE(brand_background, ''), COALESCE(brand_text, ''), COALESCE(brand_button, ''), COALESCE(brand_highlight, ''), "
            + "COALESCE(brand_tokens, '') "
            + "FROM sp_get_public_tenant_branding(@slug)", connection);
        cmd.Parameters.AddWithValue("slug", request.Slug);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Tenant not found"));
        }
        var logoImagesId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
        var baseUrl = configuration["PUBLIC_BASE_URL"] ?? string.Empty;
        return new PublicTenantBranding
        {
            Slug = reader.GetString(0),
            Name = reader.GetString(1),
            LogoUrl = logoImagesId is { } logo ? $"{baseUrl}/images/{logo}" : string.Empty,
            BrandPrimary = reader.GetString(3),
            BrandSecondary = reader.GetString(4),
            BrandAccent = reader.GetString(5),
            BrandBackground = reader.GetString(6),
            BrandText = reader.GetString(7),
            BrandButton = reader.GetString(8),
            BrandHighlight = reader.GetString(9),
            BrandTokensJson = reader.GetString(10)
        };
    }

    public override async Task<AckResponse> UpdateMyTenantBranding(UpdateMyTenantBrandingRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (tenantContext.UsersId is not { } usersId || tenantContext.TenantsId is not { } tenantsId)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
        object logo = DBNull.Value;
        if (Guid.TryParse(request.LogoImagesId, out var logoId))
        {
            logo = logoId;
        }
        await using var connection = await db.OpenAsync(usersId, tenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_tenant_branding(@t, @logo, @primary, @secondary, @accent, @background, @text, @button, @highlight, @tokens)", connection);
        cmd.Parameters.AddWithValue("t", tenantsId);
        cmd.Parameters.AddWithValue("logo", logo);
        cmd.Parameters.AddWithValue("primary", (object?)NullIfEmpty(request.BrandPrimary) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("secondary", (object?)NullIfEmpty(request.BrandSecondary) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("accent", (object?)NullIfEmpty(request.BrandAccent) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("background", (object?)NullIfEmpty(request.BrandBackground) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("text", (object?)NullIfEmpty(request.BrandText) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("button", (object?)NullIfEmpty(request.BrandButton) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("highlight", (object?)NullIfEmpty(request.BrandHighlight) ?? DBNull.Value);
        cmd.Parameters.Add(new NpgsqlParameter("tokens", NpgsqlTypes.NpgsqlDbType.Jsonb)
        {
            Value = (object?)NullIfEmpty(request.BrandTokensJson) ?? DBNull.Value
        });
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Tenant branding updated" };
    }

    public override async Task<AckResponse> UpdateMyTenantContact(UpdateMyTenantContactRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        if (tenantContext.UsersId is not { } usersId || tenantContext.TenantsId is not { } tenantsId)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
        await using var connection = await db.OpenAsync(usersId, tenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_tenant_contact(@t, @phone, @l1, @l2, @city, @state, @zip)", connection);
        cmd.Parameters.AddWithValue("t", tenantsId);
        cmd.Parameters.AddWithValue("phone", (object?)NullIfEmpty(request.Phone) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("l1", (object?)NullIfEmpty(request.AddressLine1) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("l2", (object?)NullIfEmpty(request.AddressLine2) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("city", (object?)NullIfEmpty(request.City) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("state", (object?)NullIfEmpty(request.State) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("zip", (object?)NullIfEmpty(request.Zip) ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Tenant contact updated" };
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
        RequireDeveloperOrOwnTenant(request.Value);
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT stripe_connected_account_id, business_name, "
            + "COALESCE(business_type, ''), COALESCE(business_url, ''), "
            + "COALESCE(product_description, ''), COALESCE(mcc, ''), COALESCE(support_email, '') "
            + "FROM vw_tenant_stripe_profile WHERE tenants_id = @t", connection);
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

        string? accountId;
        await using (var cmd = new NpgsqlCommand(
            "SELECT sp_update_tenant_legal_name(@t, @bizname)", connection))
        {
            cmd.Parameters.AddWithValue("bizname", (object?)NullIfEmpty(request.BusinessName) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("t", tenantsId);
            accountId = await cmd.ExecuteScalarAsync(ct) as string;
        }

        await using (var cmd = new NpgsqlCommand(
            "SELECT sp_upsert_tenant_stripe_profile(@t, @bt, @url, @desc, @mcc, @email)", connection))
        {
            cmd.Parameters.AddWithValue("bt", (object?)NullIfEmpty(request.BusinessType) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("url", (object?)NullIfEmpty(request.BusinessUrl) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.ProductDescription) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("mcc", (object?)NullIfEmpty(request.Mcc) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("email", (object?)NullIfEmpty(request.SupportEmail) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("t", tenantsId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

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
