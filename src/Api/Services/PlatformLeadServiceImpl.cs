using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Admin;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class PlatformLeadServiceImpl : PlatformLeadService.PlatformLeadServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public PlatformLeadServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<UuidValue> CreatePlatformLead(CreatePlatformLeadRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.Phone) ||
            string.IsNullOrWhiteSpace(request.Description))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Name, company, phone, and description are required"));
        }
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_platform_lead(@name, @company, @phone, @website, @description)", connection);
        cmd.Parameters.AddWithValue("name", request.Name.Trim());
        cmd.Parameters.AddWithValue("company", request.CompanyName.Trim());
        cmd.Parameters.AddWithValue("phone", request.Phone.Trim());
        cmd.Parameters.AddWithValue("website", (object?)NullIfEmpty(request.Website?.Trim()) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("description", request.Description.Trim());
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<ListPlatformLeadsResponse> ListPlatformLeads(PageRequest request, ServerCallContext context)
    {
        if (!tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }
        var ct = context.CancellationToken;
        var response = new ListPlatformLeadsResponse { Meta = new PageMeta() };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT platform_leads_id, name, company_name, phone, website, description, created_at FROM vw_platform_leads ORDER BY created_at DESC", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Leads.Add(new PlatformLead
            {
                PlatformLeadsId = reader.GetGuid(0).ToString(),
                Name = reader.GetString(1),
                CompanyName = reader.GetString(2),
                Phone = reader.GetString(3),
                Website = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Description = reader.GetString(5),
                CreatedAt = new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero).ToUnixTimeSeconds()
            });
        }
        response.Meta.Total = response.Leads.Count;
        return response;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
