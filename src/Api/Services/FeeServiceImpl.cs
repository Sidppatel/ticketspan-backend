using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Fees;

namespace TicketSpan.Api.Services;

public sealed class FeeServiceImpl : FeeService.FeeServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public FeeServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<ListFeeFormulasResponse> ListFeeFormulas(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        var response = new ListFeeFormulasResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT fee_formulas_id, name, percent_bps, flat_cents, "
            + "min_fee_cents, max_fee_cents, is_active "
            + "FROM vw_fee_formulas ORDER BY is_active DESC, name", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Formulas.Add(MapFormula(reader));
        }
        return response;
    }

    private static FeeFormula MapFormula(NpgsqlDataReader r) => new()
    {
        FeeFormulasId = r.GetGuid(0).ToString(),
        Name = r.GetString(1),
        PercentBps = r.GetInt32(2),
        FlatCents = r.GetInt32(3),
        MinFeeCents = r.GetInt32(4),
        MaxFeeCents = r.GetInt32(5),
        IsActive = r.GetBoolean(6)
    };

    public override async Task<UuidValue> CreateFeeFormula(FeeFormulaInput request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireDeveloper();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_fee_formula(@name, @pct, @flat, @min, @max)", connection);
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("pct", request.PercentBps);
        cmd.Parameters.AddWithValue("flat", request.FlatCents);
        cmd.Parameters.AddWithValue("min", request.MinFeeCents == 0 ? DBNull.Value : request.MinFeeCents);
        cmd.Parameters.AddWithValue("max", request.MaxFeeCents == 0 ? DBNull.Value : request.MaxFeeCents);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<AckResponse> UpdateFeeFormula(FeeFormula request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireDeveloper();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_fee_formula(@id, @name, @pct, @flat, @min, @max, @active)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.FeeFormulasId));
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("pct", request.PercentBps);
        cmd.Parameters.AddWithValue("flat", request.FlatCents);
        cmd.Parameters.AddWithValue("min", request.MinFeeCents == 0 ? DBNull.Value : request.MinFeeCents);
        cmd.Parameters.AddWithValue("max", request.MaxFeeCents == 0 ? DBNull.Value : request.MaxFeeCents);
        cmd.Parameters.AddWithValue("active", request.IsActive);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Fee formula updated" };
    }

    public override async Task<AckResponse> DeleteFeeFormula(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireDeveloper();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_delete_fee_formula(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Fee formula deleted" };
    }

    public override async Task<AckResponse> AssignFeeFormula(AssignFeeFormulaRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        if (request.Kind is not ("ticket" or "table"))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "kind must be 'ticket' or 'table'"));
        }
        if (tenantContext.IsDeveloper && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "An override reason is required"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        string previousFormula;
        await using (var cmd = new NpgsqlCommand("SELECT sp_set_fee_formula(@kind, @target, @formula)", connection))
        {
            cmd.Parameters.AddWithValue("kind", request.Kind);
            cmd.Parameters.AddWithValue("target", Guid.Parse(request.TargetId));
            cmd.Parameters.AddWithValue("formula", string.IsNullOrEmpty(request.FeeFormulasId) ? DBNull.Value : Guid.Parse(request.FeeFormulasId));
            var oldValue = await cmd.ExecuteScalarAsync(ct);
            previousFormula = oldValue is Guid oldId ? oldId.ToString() : string.Empty;
        }

        var metadataJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            kind = request.Kind,
            from = previousFormula,
            to = request.FeeFormulasId,
            reason = request.Reason
        });
        await using (var auditCmd = new NpgsqlCommand(
            "SELECT sp_create_audit_log('FeeOverride', @actorType, @actor, 'FeeTarget', @subject, 'fee_formula_assigned', @meta, NULL, NULL)", connection))
        {
            auditCmd.Parameters.AddWithValue("actorType", tenantContext.IsDeveloper ? "Developer" : "Admin");
            auditCmd.Parameters.AddWithValue("actor", (object?)tenantContext.UsersId ?? DBNull.Value);
            auditCmd.Parameters.AddWithValue("subject", Guid.Parse(request.TargetId));
            auditCmd.Parameters.AddWithValue("meta", metadataJson);
            await auditCmd.ExecuteNonQueryAsync(ct);
        }
        return new AckResponse { Success = true, Message = "Fee formula assigned" };
    }

    public override async Task<DeveloperEventsResponse> ListAllEvents(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireDeveloper();
        var response = new DeveloperEventsResponse();
        var byEvent = new Dictionary<string, DeveloperEvent>();

        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT events_id, tenants_id, tenant_name, title, status, "
            + "line_id, kind, label, price_cents, fee_formulas_id, fee_cents "
            + "FROM vw_event_fee_line_items "
            + "ORDER BY tenant_name, title, kind, label", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var eventsId = reader.GetGuid(0).ToString();
            if (!byEvent.TryGetValue(eventsId, out var ev))
            {
                ev = new DeveloperEvent
                {
                    EventsId = eventsId,
                    TenantsId = reader.GetGuid(1).ToString(),
                    TenantName = reader.GetString(2),
                    Title = reader.GetString(3),
                    Status = reader.GetString(4)
                };
                byEvent[eventsId] = ev;
                response.Events.Add(ev);
            }
            if (!reader.IsDBNull(5))
            {
                ev.Items.Add(new FeeLineItem
                {
                    Id = reader.GetGuid(5).ToString(),
                    Kind = reader.GetString(6),
                    Label = reader.GetString(7),
                    PriceCents = reader.GetInt32(8),
                    FeeFormulasId = reader.IsDBNull(9) ? string.Empty : reader.GetGuid(9).ToString(),
                    FeeCents = reader.GetInt32(10)
                });
            }
        }
        return response;
    }

    private void RequireUser()
    {
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
    }

    private void RequireDeveloper()
    {
        if (!tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Developer access required"));
        }
    }
}
