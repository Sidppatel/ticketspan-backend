using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Catalog;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class VenueServiceImpl : VenueService.VenueServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public VenueServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<UuidValue> CreateVenue(CreateVenueRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_venue(@t, @name, @desc, @img, @phone, @email, @web, @l1, @l2, @city, @state, @zip)", connection);
        cmd.Parameters.AddWithValue("t", tenantContext.TenantsId!);
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("img", (object?)NullIfEmpty(request.ImagePath) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", (object?)NullIfEmpty(request.Phone) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("email", (object?)NullIfEmpty(request.Email) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("web", (object?)NullIfEmpty(request.Website) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("l1", (object?)NullIfEmpty(request.Line1) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("l2", (object?)NullIfEmpty(request.Line2) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("city", (object?)NullIfEmpty(request.City) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("state", (object?)NullIfEmpty(request.State) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("zip", (object?)NullIfEmpty(request.Zip) ?? DBNull.Value);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<AckResponse> UpdateVenue(UpdateVenueRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_venue(@id, @name, @desc, NULL, @phone, @email, @web, NULL, NULL, NULL, NULL, NULL)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.VenuesId));
        cmd.Parameters.AddWithValue("name", (object?)NullIfEmpty(request.Name) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", (object?)NullIfEmpty(request.Phone) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("email", (object?)NullIfEmpty(request.Email) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("web", (object?)NullIfEmpty(request.Website) ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Venue updated" };
    }

    public override async Task<Venue> GetVenue(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT venues_id, name, description, image_path, phone, email, website, is_active FROM vw_venues WHERE venues_id = @id", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Venue not found"));
        }
        return MapVenue(reader);
    }

    public override async Task<ListVenuesResponse> ListVenues(PageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListVenuesResponse { Meta = new PageMeta { Offset = request.Offset, Limit = request.Limit } };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT venues_id, name, description, image_path, phone, email, website, is_active FROM vw_venues ORDER BY name", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Venues.Add(MapVenue(reader));
        }
        response.Meta.Total = response.Venues.Count;
        return response;
    }

    private static Venue MapVenue(NpgsqlDataReader reader) => new()
    {
        VenuesId = reader.GetGuid(0).ToString(),
        Name = reader.GetString(1),
        Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
        ImagePath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
        Phone = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
        Email = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
        Website = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
        IsActive = !reader.IsDBNull(7) && reader.GetBoolean(7)
    };

    private void RequireTenant()
    {
        if (tenantContext.TenantsId is null && !tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
