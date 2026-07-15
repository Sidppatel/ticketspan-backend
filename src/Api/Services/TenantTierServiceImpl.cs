using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Reporting;

namespace TicketSpan.Api.Services;

public sealed class TenantTierServiceImpl : TenantTierService.TenantTierServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly ReportingAccessProvider accessProvider;

    public TenantTierServiceImpl(Db db, TenantContext tenantContext, ReportingAccessProvider accessProvider)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.accessProvider = accessProvider;
    }

    public override async Task<TenantReportingAccessList> ListTenantReportingAccess(PageRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var limit = request.Limit > 0 ? Math.Min(request.Limit, 200) : 50;
        var offset = Math.Max(request.Offset, 0);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%";
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

        var response = new TenantReportingAccessList();
        await using (var countCmd = new NpgsqlCommand(
            "SELECT COUNT(*)::int FROM vw_tenant_reporting_access WHERE (@q IS NULL OR name ILIKE @q OR slug ILIKE @q)", connection))
        {
            countCmd.Parameters.Add(new NpgsqlParameter("q", NpgsqlTypes.NpgsqlDbType.Text)
            {
                Value = (object?)search ?? DBNull.Value
            });
            response.Meta = new PageMeta
            {
                Total = (int)(await countCmd.ExecuteScalarAsync(ct))!,
                Offset = offset,
                Limit = limit
            };
        }

        await using var cmd = new NpgsqlCommand(
            "SELECT tenants_id, slug, name, tier, advanced_reporting_enabled, has_advanced_reporting, archived, "
            + "ach_enabled, ach_fee_formulas_id, tax_collection_mode "
            + "FROM vw_tenant_reporting_access WHERE (@q IS NULL OR name ILIKE @q OR slug ILIKE @q) "
            + "ORDER BY name OFFSET @o LIMIT @l", connection);
        cmd.Parameters.Add(new NpgsqlParameter("q", NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = (object?)search ?? DBNull.Value
        });
        cmd.Parameters.AddWithValue("o", offset);
        cmd.Parameters.AddWithValue("l", limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Tenants.Add(new TenantReportingAccessRow
            {
                TenantsId = reader.GetGuid(0).ToString(),
                Slug = reader.GetString(1),
                Name = reader.GetString(2),
                Tier = reader.GetString(3),
                AdvancedReportingEnabled = reader.GetBoolean(4),
                HasAdvancedReporting = reader.GetBoolean(5),
                Archived = reader.GetBoolean(6),
                AchEnabled = reader.GetBoolean(7),
                AchFeeFormulasId = reader.IsDBNull(8) ? string.Empty : reader.GetGuid(8).ToString(),
                TaxCollectionMode = reader.GetString(9)
            });
        }
        return response;
    }

    public override async Task<AckResponse> SetTenantTier(SetTenantTierRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        string oldTier;
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT sp_set_tenant_tier(@t, @tier)", connection);
            cmd.Parameters.AddWithValue("t", tenantsId);
            cmd.Parameters.AddWithValue("tier", request.Tier);
            oldTier = (string)(await cmd.ExecuteScalarAsync(ct))!;
        }
        catch (PostgresException exception) when (exception.SqlState == "P0001")
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, exception.MessageText));
        }
        accessProvider.Invalidate(tenantsId);
        await LogTierAuditAsync(connection, tenantsId, "tier_changed", $"{{\"from\":\"{oldTier}\",\"to\":\"{request.Tier}\"}}", ct);
        return new AckResponse { Success = true, Message = $"Tier changed from {oldTier} to {request.Tier}" };
    }

    public override async Task<AckResponse> SetTenantAdvancedReporting(SetTenantAdvancedReportingRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        bool oldValue;
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT sp_set_tenant_advanced_reporting(@t, @e)", connection);
            cmd.Parameters.AddWithValue("t", tenantsId);
            cmd.Parameters.AddWithValue("e", request.Enabled);
            oldValue = (bool)(await cmd.ExecuteScalarAsync(ct))!;
        }
        catch (PostgresException exception) when (exception.SqlState == "P0001")
        {
            throw new RpcException(new Status(StatusCode.NotFound, exception.MessageText));
        }
        accessProvider.Invalidate(tenantsId);
        await LogTierAuditAsync(connection, tenantsId, "advanced_reporting_toggled",
            $"{{\"from\":{(oldValue ? "true" : "false")},\"to\":{(request.Enabled ? "true" : "false")}}}", ct);
        return new AckResponse { Success = true, Message = $"Advanced reporting {(request.Enabled ? "enabled" : "disabled")}" };
    }

    public override async Task<AckResponse> SetTenantAch(SetTenantAchRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A change reason is required"));
        }
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        System.Text.Json.JsonElement oldState;
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT sp_set_tenant_ach(@t, @e, @f)", connection);
            cmd.Parameters.AddWithValue("t", tenantsId);
            cmd.Parameters.AddWithValue("e", request.Enabled);
            cmd.Parameters.AddWithValue("f", string.IsNullOrEmpty(request.FeeFormulasId)
                ? DBNull.Value : Guid.Parse(request.FeeFormulasId));
            oldState = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                (string)(await cmd.ExecuteScalarAsync(ct))!);
        }
        catch (PostgresException exception) when (exception.SqlState is "P0001" or "22023")
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, exception.MessageText));
        }
        var metadataJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            from_enabled = oldState.GetProperty("enabled").GetBoolean(),
            to_enabled = request.Enabled,
            from_fee_formulas_id = oldState.GetProperty("fee_formulas_id").ValueKind == System.Text.Json.JsonValueKind.Null
                ? string.Empty : oldState.GetProperty("fee_formulas_id").GetString(),
            to_fee_formulas_id = request.FeeFormulasId,
            reason = request.Reason
        });
        await LogTierAuditAsync(connection, tenantsId, "ach_changed", metadataJson, ct);
        return new AckResponse { Success = true, Message = $"ACH {(request.Enabled ? "enabled" : "disabled")}" };
    }

    public override async Task<AckResponse> SetTenantTaxMode(SetTenantTaxModeRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A change reason is required"));
        }
        if (request.Mode is not ("platform" or "self"))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Mode must be 'platform' or 'self'"));
        }
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        System.Text.Json.JsonElement oldState;
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT sp_set_tenant_tax_mode(@t, @m)", connection);
            cmd.Parameters.AddWithValue("t", tenantsId);
            cmd.Parameters.AddWithValue("m", request.Mode);
            oldState = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                (string)(await cmd.ExecuteScalarAsync(ct))!);
        }
        catch (PostgresException exception) when (exception.SqlState is "P0001" or "22023")
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, exception.MessageText));
        }
        var metadataJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            from_mode = oldState.GetProperty("mode").GetString(),
            to_mode = request.Mode,
            reason = request.Reason
        });
        await LogTierAuditAsync(connection, tenantsId, "tax_mode_changed", metadataJson, ct);
        return new AckResponse { Success = true, Message = $"Tax collection mode set to {request.Mode}" };
    }

    private void RequireDeveloper()
    {
        if (!tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Developer access required"));
        }
    }

    private async Task LogTierAuditAsync(NpgsqlConnection connection, Guid tenantsId, string action, string metadataJson, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_audit_log('TenantTier', 'Developer', @actor, 'Tenant', @subject, @action, @meta, NULL, NULL)", connection);
        cmd.Parameters.AddWithValue("actor", (object?)tenantContext.UsersId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subject", tenantsId);
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("meta", metadataJson);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
