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
            "SELECT sp_create_venue(@t, @name, @desc, @img, @phone, @email, @web, @l1, @l2, @city, @state, @zip, @cap, @vtype)", connection);
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
        cmd.Parameters.AddWithValue("cap", DBNull.Value);
        cmd.Parameters.AddWithValue("vtype", DBNull.Value);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<AckResponse> UpdateVenue(UpdateVenueRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_venue(@id, @name, @desc, NULL, @phone, @email, @web, @active, @l1, @l2, @city, @state, @zip, @cap, @vtype)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.VenuesId));
        cmd.Parameters.AddWithValue("name", (object?)NullIfEmpty(request.Name) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("desc", (object?)NullIfEmpty(request.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", (object?)NullIfEmpty(request.Phone) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("email", (object?)NullIfEmpty(request.Email) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("web", (object?)NullIfEmpty(request.Website) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("active", request.IsActive);
        cmd.Parameters.AddWithValue("l1", (object?)NullIfEmpty(request.Line1) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("l2", (object?)NullIfEmpty(request.Line2) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("city", (object?)NullIfEmpty(request.City) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("state", (object?)NullIfEmpty(request.State) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("zip", (object?)NullIfEmpty(request.Zip) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cap", DBNull.Value);
        cmd.Parameters.AddWithValue("vtype", DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Venue updated" };
    }

    public override async Task<Venue> GetVenue(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(VenueSelect + " WHERE venues_id = @id", connection);
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
        await using var cmd = new NpgsqlCommand(VenueSelect + " ORDER BY name", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Venues.Add(MapVenue(reader));
        }
        response.Meta.Total = response.Venues.Count;
        return response;
    }

    public override async Task<ListVenueImagesResponse> ListVenueImages(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListVenueImagesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT images_id, storage_key, is_primary, sort_order FROM sp_list_venue_images(@v)", connection);
        cmd.Parameters.AddWithValue("v", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Images.Add(new VenueImage
            {
                ImagesId = reader.GetGuid(0).ToString(),
                StorageKey = reader.GetString(1),
                IsPrimary = reader.GetBoolean(2),
                SortOrder = reader.GetInt32(3)
            });
        }
        return response;
    }

    public override async Task<VenueImage> AddVenueImage(AddVenueImageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT i.storage_key, l.is_primary, l.sort_order "
            + "FROM sp_link_venue_image(@v, @img) l JOIN images i ON i.images_id = @img", connection);
        cmd.Parameters.AddWithValue("v", Guid.Parse(request.VenuesId));
        cmd.Parameters.AddWithValue("img", Guid.Parse(request.ImagesId));
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to link image"));
            }
            return new VenueImage
            {
                ImagesId = request.ImagesId,
                StorageKey = reader.GetString(0),
                IsPrimary = reader.GetBoolean(1),
                SortOrder = reader.GetInt32(2)
            };
        }
        catch (PostgresException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText));
        }
    }

    public override async Task<AckResponse> RemoveVenueImage(RemoveVenueImageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_remove_venue_image(@v, @img)", connection);
        cmd.Parameters.AddWithValue("v", Guid.Parse(request.VenuesId));
        cmd.Parameters.AddWithValue("img", Guid.Parse(request.ImagesId));
        var ok = (bool)(await cmd.ExecuteScalarAsync(ct))!;
        return new AckResponse { Success = ok, Message = ok ? "Image removed" : "Image not found" };
    }

    public override async Task<AckResponse> SetPrimaryVenueImage(RemoveVenueImageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_venue_primary_image(@v, @img)", connection);
        cmd.Parameters.AddWithValue("v", Guid.Parse(request.VenuesId));
        cmd.Parameters.AddWithValue("img", Guid.Parse(request.ImagesId));
        var ok = (bool)(await cmd.ExecuteScalarAsync(ct))!;
        return new AckResponse { Success = ok, Message = ok ? "Primary image set" : "Image not found" };
    }

    public override async Task<AckResponse> ReorderVenueImages(ReorderVenueImagesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var ids = request.ImagesId.Select(Guid.Parse).ToArray();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_reorder_venue_images(@v, @ids)", connection);
        cmd.Parameters.AddWithValue("v", Guid.Parse(request.VenuesId));
        cmd.Parameters.AddWithValue("ids", ids);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Images reordered" };
    }

    private const string VenueSelect =
        "SELECT venues_id, name, description, image_path, phone, email, website, is_active, state, "
        + "address_line1, address_line2, city, zip_code, capacity, venue_type FROM vw_venues";

    private static Venue MapVenue(NpgsqlDataReader reader) => new()
    {
        VenuesId = reader.GetGuid(0).ToString(),
        Name = reader.GetString(1),
        Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
        ImagePath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
        Phone = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
        Email = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
        Website = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
        IsActive = !reader.IsDBNull(7) && reader.GetBoolean(7),
        State = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
        Line1 = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
        Line2 = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
        City = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
        Zip = reader.IsDBNull(12) ? string.Empty : reader.GetString(12)
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
