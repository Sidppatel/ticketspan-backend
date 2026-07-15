using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Pricing;

namespace TicketSpan.Api.Services;

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
            "SELECT sp_create_price(@ev, @name, @type, @base, @per, @allinc, @fee, @max)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.EventsId));
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("type", string.IsNullOrEmpty(request.PricingType) ? "TicketTier" : request.PricingType);
        cmd.Parameters.AddWithValue("base", request.BasePriceCents);
        cmd.Parameters.AddWithValue("per", request.PerAttendeeCents);
        cmd.Parameters.AddWithValue("allinc", request.IsAllInclusive);
        cmd.Parameters.AddWithValue("fee",
            tenantContext.IsDeveloper && !string.IsNullOrEmpty(request.FeeFormulasId)
                ? Guid.Parse(request.FeeFormulasId) : (object)DBNull.Value);
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
        await using var cmd = new NpgsqlCommand("SELECT prices_id, events_id, name, pricing_type, base_price_cents, per_attendee_cents, is_all_inclusive, fee_formulas_id, max_quantity, is_active FROM sp_get_price(@id)", connection);
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
        var response = new ListPricesResponse();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await EventAccess.RequireAsync(connection, tenantContext, Guid.Parse(request.Value), ct);
        await using var cmd = new NpgsqlCommand("SELECT prices_id, events_id, name, pricing_type, base_price_cents, per_attendee_cents, is_all_inclusive, fee_formulas_id, max_quantity, is_active FROM sp_list_prices_for_event(@ev)", connection);
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
            "SELECT sp_create_price_rule(@owner, @name, @type, @prio, @price, @from, @until, @min, @max, @cap, @scope)", connection);
        cmd.Parameters.AddWithValue("owner", Guid.Parse(request.OwnerId));
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("type", string.IsNullOrEmpty(request.RuleType) ? "TimeWindow" : request.RuleType);
        cmd.Parameters.AddWithValue("prio", request.Priority);
        cmd.Parameters.AddWithValue("price", request.PriceCents);
        cmd.Parameters.AddWithValue("from", ToTimestamp(request.ActiveFrom));
        cmd.Parameters.AddWithValue("until", ToTimestamp(request.ActiveUntil));
        cmd.Parameters.AddWithValue("min", request.MinRemaining < 0 ? DBNull.Value : request.MinRemaining);
        cmd.Parameters.AddWithValue("max", request.MaxRemaining < 0 ? DBNull.Value : request.MaxRemaining);
        cmd.Parameters.AddWithValue("cap", request.Capacity == 0 ? DBNull.Value : request.Capacity);
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
            "SELECT sp_update_price_rule(@id, @name, @type, @prio, @price, @from, @until, @min, @max, @active, @cap)", connection);
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
        cmd.Parameters.AddWithValue("cap", request.Capacity == 0 ? DBNull.Value : request.Capacity);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Price rule updated" };
    }

    public override async Task<AckResponse> DeletePriceRule(UuidValue request, ServerCallContext context)
        => await RunVoid("SELECT sp_delete_price_rule(@id)", "id", Guid.Parse(request.Value), context, "Price rule deleted");

    public override Task<ListPriceRulesResponse> ListPriceRules(UuidValue request, ServerCallContext context)
        => ListRules("SELECT price_rules_id, prices_id, name, rule_type, priority, price_cents, active_from, active_until, min_remaining, max_remaining, is_active, scope, events_id, capacity FROM sp_list_price_rules(@id)", request, context);

    public override Task<ListPriceRulesResponse> ListEventPriceRules(UuidValue request, ServerCallContext context)
        => ListRules("SELECT price_rules_id, prices_id, name, rule_type, priority, price_cents, active_from, active_until, min_remaining, max_remaining, is_active, scope, events_id, capacity FROM sp_list_event_price_rules(@id)", request, context);

    private async Task<ListPriceRulesResponse> ListRules(string sql, UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
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
                EventsId = reader.IsDBNull(12) ? string.Empty : reader.GetGuid(12).ToString(),
                Capacity = reader.IsDBNull(13) ? 0 : reader.GetInt32(13)
            });
        }
        return response;
    }

    public override async Task<PriceBreakdown> CalculatePrice(CalculatePriceRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT base_price_cents, selling_price_cents, discount_cents, applied_price_rules_id, applied_rule_name, platform_fee_cents, gateway_fee_cents, tax_cents, final_price_cents, organizer_net_cents, currency FROM sp_calculate_price(@p, @seats, @at, @rem)", connection);
        cmd.Parameters.AddWithValue("p", Guid.Parse(request.PricesId));
        cmd.Parameters.AddWithValue("seats", request.Seats <= 0 ? 1 : request.Seats);
        cmd.Parameters.AddWithValue("at", ToTimestamp(request.At));
        cmd.Parameters.AddWithValue("rem", request.Remaining < 0 ? DBNull.Value : request.Remaining);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Price not found or inactive"));
        }
        return MapBreakdown(reader);
    }

    internal static PriceBreakdown MapBreakdown(NpgsqlDataReader r)
    {
        var bd = new PriceBreakdown
        {
            BasePriceCents = r.GetInt32(0),
            SellingPriceCents = r.GetInt32(1),
            DiscountCents = r.GetInt32(2),
            AppliedPriceRulesId = r.IsDBNull(3) ? string.Empty : r.GetGuid(3).ToString(),
            AppliedRuleName = r.IsDBNull(4) ? string.Empty : r.GetString(4),
            PlatformFeeCents = r.GetInt32(5),
            GatewayFeeCents = r.GetInt32(6),
            TaxCents = r.GetInt32(7),
            FinalPriceCents = r.GetInt32(8),
            OrganizerNetCents = r.GetInt32(9),
            Currency = r.IsDBNull(10) ? "usd" : r.GetString(10)
        };
        bd.SubtotalCents = bd.SellingPriceCents;
        bd.FeeCents = bd.PlatformFeeCents + bd.GatewayFeeCents + bd.TaxCents;
        bd.TotalCents = bd.FinalPriceCents;
        return bd;
    }

    public override async Task<AckResponse> SetTenantDefaultFeeFormula(SetTenantDefaultFeeFormulaRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        RequireDeveloper();
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "An override reason is required"));
        }
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        string previousFormula;
        await using (var cmd = new NpgsqlCommand("SELECT sp_set_tenant_default_fee_formula(@t, @fee)", connection))
        {
            cmd.Parameters.AddWithValue("t", Guid.Parse(request.TenantsId));
            cmd.Parameters.AddWithValue("fee", string.IsNullOrEmpty(request.FeeFormulasId) ? DBNull.Value : Guid.Parse(request.FeeFormulasId));
            var oldValue = await cmd.ExecuteScalarAsync(ct);
            previousFormula = oldValue is Guid oldId ? oldId.ToString() : string.Empty;
        }

        var metadataJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            from = previousFormula,
            to = request.FeeFormulasId,
            reason = request.Reason
        });
        await using (var auditCmd = new NpgsqlCommand(
            "SELECT sp_create_audit_log('FeeOverride', 'Developer', @actor, 'Tenant', @subject, 'tenant_default_fee_changed', @meta, NULL, NULL)", connection))
        {
            auditCmd.Parameters.AddWithValue("actor", (object?)tenantContext.UsersId ?? DBNull.Value);
            auditCmd.Parameters.AddWithValue("subject", Guid.Parse(request.TenantsId));
            auditCmd.Parameters.AddWithValue("meta", metadataJson);
            await auditCmd.ExecuteNonQueryAsync(ct);
        }
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
        MaxQuantity = r.IsDBNull(8) ? 0 : r.GetInt32(8),
        IsActive = r.GetBoolean(9)
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
