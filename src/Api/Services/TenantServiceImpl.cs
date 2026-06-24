using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Tenant;

namespace Svyne.Api.Services;

public sealed class TenantServiceImpl : TenantService.TenantServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly IConfiguration configuration;

    public TenantServiceImpl(Db db, TenantContext tenantContext, IConfiguration configuration)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.configuration = configuration;
    }

    public override async Task<CreateTenantResponse> CreateTenant(CreateTenantRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var emailHash = EmailHasher.Hash(request.AdminEmail);
        var magicToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var magicHash = EmailHasher.Hash(magicToken);

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
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddDays(7));
        cmd.Parameters.AddWithValue("legal", (object?)NullIfEmpty(request.LegalName) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cc", string.IsNullOrEmpty(request.CountryCode) ? "US" : request.CountryCode);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.Internal, "Tenant creation failed"));
        }
        var tenantsId = reader.GetGuid(0);
        var adminUsersId = reader.GetGuid(1);
        var baseUrl = configuration["PUBLIC_BASE_URL"] ?? "https://localhost";
        return new CreateTenantResponse
        {
            TenantsId = tenantsId.ToString(),
            AdminUsersId = adminUsersId.ToString(),
            SetupUrl = $"{baseUrl}/setup?token={magicToken}"
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
            "SELECT tenants_id, slug, name, legal_name, country_code, member_count, event_count, total_revenue_cents, archived_at IS NOT NULL "
            + "FROM vw_tenants WHERE tenants_id = @id", connection);
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
            Archived = reader.GetBoolean(8)
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

    private void RequireDeveloper()
    {
        if (!tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Developer access required"));
        }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
