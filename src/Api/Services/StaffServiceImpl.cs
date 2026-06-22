using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Admin;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class StaffServiceImpl : StaffService.StaffServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public StaffServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<ListStaffResponse> ListStaffForEvent(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListStaffResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT users_id, first_name, last_name, email FROM sp_list_staff_for_event(@ev)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Staff.Add(new StaffMember
            {
                UsersId = reader.GetGuid(0).ToString(),
                FirstName = reader.GetString(1),
                LastName = reader.GetString(2),
                Email = reader.GetString(3)
            });
        }
        return response;
    }

    public override async Task<AckResponse> AssignStaff(AssignStaffRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_assign_user_event(@u, @ev, @by)", connection);
        cmd.Parameters.AddWithValue("u", Guid.Parse(request.UsersId));
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("by", (object?)tenantContext.UsersId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Staff assigned" };
    }

    public override async Task<AckResponse> UnassignStaff(AssignStaffRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_unassign_user_event(@u, @ev)", connection);
        cmd.Parameters.AddWithValue("u", Guid.Parse(request.UsersId));
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Staff unassigned" };
    }

    private void RequireTenant()
    {
        if (tenantContext.TenantsId is null && !tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
    }
}
