using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Catalog;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class SponsorServiceImpl : SponsorService.SponsorServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public SponsorServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<UuidValue> CreateSponsor(CreateSponsorRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_sponsor(@t, @name, @slug, @img, @meta::jsonb, @active)", connection);
        cmd.Parameters.AddWithValue("t", tenantContext.TenantsId!);
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("slug", request.Slug);
        cmd.Parameters.AddWithValue("img", (object?)NullIfEmpty(request.ImagePath) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("meta", string.IsNullOrEmpty(request.MetaJson) ? "[]" : request.MetaJson);
        cmd.Parameters.AddWithValue("active", request.IsActive);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<AckResponse> UpdateSponsor(UpdateSponsorRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_sponsor(@id, @name, NULL, @img, @meta::jsonb, @active)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.SponsorsId));
        cmd.Parameters.AddWithValue("name", (object?)NullIfEmpty(request.Name) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("img", (object?)NullIfEmpty(request.ImagePath) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("meta", (object?)NullIfEmpty(request.MetaJson) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("active", request.IsActive);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Sponsor updated" };
    }

    public override async Task<AckResponse> DeleteSponsor(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_delete_sponsor(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Sponsor deleted" };
    }

    public override async Task<ListSponsorsResponse> ListSponsors(PageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListSponsorsResponse { Meta = new PageMeta { Offset = request.Offset, Limit = request.Limit } };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sponsors_id, name, slug, primary_image_path, meta::text, is_active FROM vw_sponsors ORDER BY name", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Sponsors.Add(new Sponsor
            {
                SponsorsId = reader.GetGuid(0).ToString(),
                Name = reader.GetString(1),
                Slug = reader.GetString(2),
                PrimaryImagePath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                MetaJson = reader.IsDBNull(4) ? "[]" : reader.GetString(4),
                IsActive = reader.GetBoolean(5)
            });
        }
        response.Meta.Total = response.Sponsors.Count;
        return response;
    }

    public override async Task<AckResponse> SetEventSponsors(SetEventLinksRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_event_sponsors(@ev, @links::jsonb)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("links", string.IsNullOrEmpty(request.LinksJson) ? "[]" : request.LinksJson);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Sponsors updated" };
    }

    private void RequireTenant()
    {
        if (tenantContext.TenantsId is null && !tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
