using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Pricing;

namespace Svyne.Api.Services;

/// <summary>
/// The Pricing Module gRPC surface — the single source of truth for all pricing.
/// Prices and priority-ordered rules (presale / last-minute / dynamic) are managed
/// here; CalculatePrice exposes the server-authoritative breakdown consumed by the
/// floor plan and checkout. Fee-formula overrides (per price and the tenant
/// default) are developer-only; admins may see but not change them.
/// </summary>
public sealed class PricingServiceImpl : PricingService.PricingServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public PricingServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<UuidValue> CreatePrice(CreatePriceRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_price(@ev, @name, @type, @base, @per, @allinc, @fee, @parent, @max)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("type", string.IsNullOrEmpty(request.PricingType) ? "TicketTier" : request.PricingType);
        cmd.Parameters.AddWithValue("base", request.BasePriceCents);
        cmd.Parameters.AddWithValue("per", request.PerAttendeeCents);
        cmd.Parameters.AddWithValue("allinc", request.IsAllInclusive);
        // Fee override honored only for developers; admins fall back to the tenant default.
        cmd.Parameters.AddWithValue("fee",
            tenantContext.IsDeveloper && !string.IsNullOrEmpty(request.FeeFormulasId)
                ? Guid.Parse(request.FeeFormulasId) : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("parent", string.IsNullOrEmpty(request.ParentPricesId) ? DBNull.Value : Guid.Parse(request.ParentPricesId));
        cmd.Parameters.AddWithValue("max", request.MaxQuantity == 0 ? DBNull.Value : request.MaxQuantity);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<AckResponse> UpdatePrice(UpdatePriceRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_price(@id, @name, @base, @per, @allinc, @max, @active, @fee, @allow)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.PricesId));
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("base", request.BasePriceCents);
        cmd.Parameters.AddWithValue("per", request.PerAttendeeCents);
        cmd.Parameters.AddWithValue("allinc", request.IsAllInclusive);
        cmd.Parameters.AddWithValue("max", request.MaxQuantity == 0 ? DBNull.Value : request.MaxQuantity);
        cmd.Parameters.AddWithValue("active", request.IsActive);
        cmd.Parameters.AddWithValue("fee", string.IsNullOrEmpty(request.FeeFormulasId) ? DBNull.Value : Guid.Parse(request.FeeFormulasId));
        cmd.Parameters.AddWithValue("allow", tenantContext.IsDeveloper);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Price updated" };
    }

    public override async Task<Price> GetPrice(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM sp_get_price(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Price not found"));
        }
        return MapPrice(reader);
    }

    public override async Task<ListPricesResponse> ListPricesForEvent(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        var response = new ListPricesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM sp_list_prices_for_event(@ev)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Prices.Add(MapPrice(reader));
        }
        return response;
    }

    public override async Task<AckResponse> DeletePrice(UuidValue request, ServerCallContext context)
        => await RunVoid("SELECT sp_delete_price(@id)", "id", Guid.Parse(request.Value), context, "Price deleted");

    public override async Task<UuidValue> CreatePriceRule(CreatePriceRuleRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_price_rule(@owner, @name, @type, @prio, @price, @from, @until, @min, @max, @scope)", connection);
        cmd.Parameters.AddWithValue("owner", Guid.Parse(request.OwnerId));
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("type", string.IsNullOrEmpty(request.RuleType) ? "TimeWindow" : request.RuleType);
        cmd.Parameters.AddWithValue("prio", request.Priority);
        cmd.Parameters.AddWithValue("price", request.PriceCents);
        cmd.Parameters.AddWithValue("from", ToTimestamp(request.ActiveFrom));
        cmd.Parameters.AddWithValue("until", ToTimestamp(request.ActiveUntil));
        cmd.Parameters.AddWithValue("min", request.MinRemaining < 0 ? DBNull.Value : request.MinRemaining);
        cmd.Parameters.AddWithValue("max", request.MaxRemaining < 0 ? DBNull.Value : request.MaxRemaining);
        cmd.Parameters.AddWithValue("scope", string.IsNullOrEmpty(request.Scope) ? "Price" : request.Scope);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<AckResponse> UpdatePriceRule(UpdatePriceRuleRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_update_price_rule(@id, @name, @type, @prio, @price, @from, @until, @min, @max, @active)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.PriceRulesId));
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("type", string.IsNullOrEmpty(request.RuleType) ? "TimeWindow" : request.RuleType);
        cmd.Parameters.AddWithValue("prio", request.Priority);
        cmd.Parameters.AddWithValue("price", request.PriceCents);
        cmd.Parameters.AddWithValue("from", ToTimestamp(request.ActiveFrom));
        cmd.Parameters.AddWithValue("until", ToTimestamp(request.ActiveUntil));
        cmd.Parameters.AddWithValue("min", request.MinRemaining < 0 ? DBNull.Value : request.MinRemaining);
        cmd.Parameters.AddWithValue("max", request.MaxRemaining < 0 ? DBNull.Value : request.MaxRemaining);
        cmd.Parameters.AddWithValue("active", request.IsActive);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Price rule updated" };
    }

    public override async Task<AckResponse> DeletePriceRule(UuidValue request, ServerCallContext context)
        => await RunVoid("SELECT sp_delete_price_rule(@id)", "id", Guid.Parse(request.Value), context, "Price rule deleted");

    public override Task<ListPriceRulesResponse> ListPriceRules(UuidValue request, ServerCallContext context)
        => ListRules("SELECT * FROM sp_list_price_rules(@id)", request, context);

    public override Task<ListPriceRulesResponse> ListEventPriceRules(UuidValue request, ServerCallContext context)
        => ListRules("SELECT * FROM sp_list_event_price_rules(@id)", request, context);

    private async Task<ListPriceRulesResponse> ListRules(string sql, UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireUser();
        var response = new ListPriceRulesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Rules.Add(new PriceRule
            {
                PriceRulesId = reader.GetGuid(0).ToString(),
                PricesId = reader.IsDBNull(1) ? string.Empty : reader.GetGuid(1).ToString(),
                Name = reader.GetString(2),
                RuleType = reader.GetString(3),
                Priority = reader.GetInt32(4),
                PriceCents = reader.GetInt32(5),
                ActiveFrom = FromTimestamp(reader, 6),
                ActiveUntil = FromTimestamp(reader, 7),
                MinRemaining = reader.IsDBNull(8) ? -1 : reader.GetInt32(8),
                MaxRemaining = reader.IsDBNull(9) ? -1 : reader.GetInt32(9),
                IsActive = reader.GetBoolean(10),
                Scope = reader.IsDBNull(11) ? "Price" : reader.GetString(11),
                EventsId = reader.IsDBNull(12) ? string.Empty : reader.GetGuid(12).ToString()
            });
        }
        return response;
    }

    public override async Task<PriceBreakdown> CalculatePrice(CalculatePriceRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        // Open to anonymous buyers (public floor-plan price preview). RLS still
        // scopes reads to the request's resolved tenant.
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM sp_calculate_price(@p, @seats, @at, @rem)", connection);
        cmd.Parameters.AddWithValue("p", Guid.Parse(request.PricesId));
        cmd.Parameters.AddWithValue("seats", request.Seats <= 0 ? 1 : request.Seats);
        cmd.Parameters.AddWithValue("at", ToTimestamp(request.At));
        cmd.Parameters.AddWithValue("rem", request.Remaining < 0 ? DBNull.Value : request.Remaining);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Price not found or inactive"));
        }
        return new PriceBreakdown
        {
            SubtotalCents = reader.GetInt32(0),
            FeeCents = reader.GetInt32(1),
            TotalCents = reader.GetInt32(2)
        };
    }

    public override async Task<AckResponse> SetTenantDefaultFeeFormula(SetTenantDefaultFeeFormulaRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireDeveloper();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_tenant_default_fee_formula(@t, @fee)", connection);
        cmd.Parameters.AddWithValue("t", Guid.Parse(request.TenantsId));
        cmd.Parameters.AddWithValue("fee", string.IsNullOrEmpty(request.FeeFormulasId) ? DBNull.Value : Guid.Parse(request.FeeFormulasId));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Tenant default fee formula set" };
    }

    private static Price MapPrice(NpgsqlDataReader r) => new()
    {
        PricesId = r.GetGuid(0).ToString(),
        EventsId = r.GetGuid(1).ToString(),
        Name = r.GetString(2),
        PricingType = r.GetString(3),
        BasePriceCents = r.GetInt32(4),
        PerAttendeeCents = r.GetInt32(5),
        IsAllInclusive = r.GetBoolean(6),
        FeeFormulasId = r.IsDBNull(7) ? string.Empty : r.GetGuid(7).ToString(),
        ParentPricesId = r.IsDBNull(8) ? string.Empty : r.GetGuid(8).ToString(),
        MaxQuantity = r.IsDBNull(9) ? 0 : r.GetInt32(9),
        IsActive = r.GetBoolean(10)
    };

    private static object ToTimestamp(long unixSeconds) =>
        unixSeconds <= 0 ? DBNull.Value : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

    private static long FromTimestamp(NpgsqlDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? 0 : new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(ordinal), DateTimeKind.Utc)).ToUnixTimeSeconds();

    private async Task<AckResponse> RunVoid(string sql, string param, Guid id, ServerCallContext context, string okMessage)
    {
        var ct = context.CancellationToken;
        RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue(param, id);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = okMessage };
    }

    private void RequireUser()
    {
        if (tenantContext.UsersId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }
    }

    private void RequireTenant()
    {
        if (tenantContext.TenantsId is null && !tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
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
