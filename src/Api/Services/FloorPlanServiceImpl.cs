using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.FloorPlan;

namespace Svyne.Api.Services;

/// <summary>
/// Reusable whole-floor-plan templates (grid + tables + objects). Per-table
/// overrides and layout objects are persisted through
/// TableBookingService.SaveEventLayout; this service snapshots and re-applies a
/// complete plan across events.
/// </summary>
public sealed class FloorPlanServiceImpl : FloorPlanService.FloorPlanServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public FloorPlanServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<UuidValue> SaveAsTemplate(SaveAsTemplateRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_create_floor_plan_template(@ev, @name)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("name", request.Name);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<ListFloorPlanTemplatesResponse> ListFloorPlanTemplates(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        var response = new ListFloorPlanTemplatesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM sp_list_floor_plan_templates()", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Templates.Add(new FloorPlanTemplate
            {
                FloorPlanTemplatesId = reader.GetGuid(0).ToString(),
                Name = reader.GetString(1),
                TableCount = reader.GetInt32(2),
                ObjectCount = reader.GetInt32(3),
                IsActive = reader.GetBoolean(4)
            });
        }
        return response;
    }

    public override async Task<AckResponse> ApplyTemplate(ApplyTemplateRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_apply_floor_plan_template(@tpl, @ev)", connection);
        cmd.Parameters.AddWithValue("tpl", Guid.Parse(request.FloorPlanTemplatesId));
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Template applied" };
    }

    public override async Task<AckResponse> DeleteFloorPlanTemplate(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_delete_floor_plan_template(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Template deleted" };
    }

    private void RequireTenant()
    {
        if (tenantContext.TenantsId is null && !tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
        }
    }
}
