using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Fees;

namespace Svyne.Api.Services;

/// <summary>
/// Developer-managed service-fee formulas and cross-tenant event overview.
/// Reads of formulas are open to any authenticated user (tenants pick one,
/// UIs render the split); writes and the cross-tenant event list are
/// developer-only. Cross-tenant reads rely on the existing RLS policies that
/// grant developers (app.is_developer()) access to every tenant's rows.
/// </summary>
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
            + "COALESCE(min_fee_cents, 0), COALESCE(max_fee_cents, 0), is_active "
            + "FROM fee_formulas ORDER BY is_active DESC, name", connection);
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
        // Developer or the owning tenant may attach a formula. RLS still enforces
        // that a tenant can only touch its own ticket types / tables.
        RequireUser();
        if (request.Kind is not ("ticket" or "table"))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "kind must be 'ticket' or 'table'"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_fee_formula(@kind, @target, @formula)", connection);
        cmd.Parameters.AddWithValue("kind", request.Kind);
        cmd.Parameters.AddWithValue("target", Guid.Parse(request.TargetId));
        cmd.Parameters.AddWithValue("formula", string.IsNullOrEmpty(request.FeeFormulasId) ? DBNull.Value : Guid.Parse(request.FeeFormulasId));
        await cmd.ExecuteNonQueryAsync(ct);
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
            "SELECT e.events_id, e.tenants_id, t.name, e.title, e.status, "
            + "       li.id, li.kind, li.label, li.price_cents, li.fee_formulas_id, li.fee_cents "
            + "FROM events e "
            + "JOIN tenants t ON t.tenants_id = e.tenants_id "
            + "LEFT JOIN ( "
            + "    SELECT event_ticket_types_id AS id, events_id, 'ticket' AS kind, label, "
            + "           price_cents, fee_formulas_id, COALESCE(platform_fee_cents, 0) AS fee_cents "
            + "    FROM event_ticket_types WHERE is_active = true "
            + "    UNION ALL "
            + "    SELECT event_tables_id AS id, events_id, 'table' AS kind, label, "
            + "           price_cents, fee_formulas_id, COALESCE(platform_fee_cents, 0) AS fee_cents "
            + "    FROM event_tables WHERE is_active = true "
            + ") li ON li.events_id = e.events_id "
            + "ORDER BY t.name, e.title, li.kind, li.label", connection);
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
