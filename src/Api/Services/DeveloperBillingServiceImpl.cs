using System.Text.Json;
using Grpc.Core;
using Npgsql;
using NpgsqlTypes;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Billing;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

/// <summary>
/// Developer-only pricing controls: trials, subscriptions, Pay Per Event,
/// add-ons, fee overrides, and platform revenue reports. State transitions live
/// in sp_billing.sql; this layer gates access, audits every mutation, and
/// invalidates the reporting-access cache on tier changes.
/// </summary>
public sealed class DeveloperBillingServiceImpl : DeveloperBillingService.DeveloperBillingServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly ReportingAccessProvider accessProvider;

    public DeveloperBillingServiceImpl(Db db, TenantContext tenantContext, ReportingAccessProvider accessProvider)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.accessProvider = accessProvider;
    }

    public override async Task<TenantBillingList> ListTenantBilling(PageRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var limit = request.Limit > 0 ? Math.Min(request.Limit, 200) : 50;
        var offset = Math.Max(request.Offset, 0);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%";
        await using var connection = await OpenAsync(ct);

        var response = new TenantBillingList();
        await using (var countCmd = new NpgsqlCommand(
            "SELECT COUNT(*)::int FROM vw_tenant_billing WHERE (@q IS NULL OR name ILIKE @q OR slug ILIKE @q)", connection))
        {
            countCmd.Parameters.Add(TextParam("q", search));
            response.Meta = new PageMeta
            {
                Total = (int)(await countCmd.ExecuteScalarAsync(ct))!,
                Offset = offset,
                Limit = limit
            };
        }

        await using var cmd = new NpgsqlCommand(
            "SELECT tenants_id, slug, name, tier, archived, subscription_status, subscription_tier, "
            + "monthly_price_cents, current_period_end, cancel_at_period_end, pending_tier, trial_ends_at, "
            + "fee_percent_bps, fee_flat_cents, has_custom_fee_override, active_addons, total_events "
            + "FROM vw_tenant_billing WHERE (@q IS NULL OR name ILIKE @q OR slug ILIKE @q) "
            + "ORDER BY name OFFSET @o LIMIT @l", connection);
        cmd.Parameters.Add(TextParam("q", search));
        cmd.Parameters.AddWithValue("o", offset);
        cmd.Parameters.AddWithValue("l", limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Tenants.Add(new TenantBillingRow
            {
                TenantsId = reader.GetGuid(0).ToString(),
                Slug = reader.GetString(1),
                Name = reader.GetString(2),
                Tier = reader.GetString(3),
                Archived = reader.GetBoolean(4),
                SubscriptionStatus = reader.IsDBNull(5) ? "" : reader.GetString(5),
                SubscriptionTier = reader.IsDBNull(6) ? "" : reader.GetString(6),
                MonthlyPriceCents = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                CurrentPeriodEndEpochSeconds = EpochOrZero(reader, 8),
                CancelAtPeriodEnd = !reader.IsDBNull(9) && reader.GetBoolean(9),
                PendingTier = reader.IsDBNull(10) ? "" : reader.GetString(10),
                TrialEndsAtEpochSeconds = EpochOrZero(reader, 11),
                FeePercentBps = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                FeeFlatCents = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                HasCustomFeeOverride = !reader.IsDBNull(14) && reader.GetBoolean(14),
                ActiveAddons = reader.GetInt32(15),
                TotalEvents = reader.GetInt32(16)
            });
        }
        return response;
    }

    public override async Task<AckResponse> StartTrial(TenantRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_start_trial(@t)",
            cmd => cmd.Parameters.AddWithValue("t", tenantsId), ct);
        accessProvider.Invalidate(tenantsId);
        await AuditAsync(connection, "Billing", "Tenant", tenantsId, "trial_started",
            new { days = 14 }, ct);
        return Ack("14-day trial started");
    }

    public override async Task<AckResponse> CreateSubscription(SubscriptionRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_create_subscription(@t, @tier)", cmd =>
        {
            cmd.Parameters.AddWithValue("t", tenantsId);
            cmd.Parameters.AddWithValue("tier", request.Tier);
        }, ct);
        accessProvider.Invalidate(tenantsId);
        await AuditAsync(connection, "Billing", "Tenant", tenantsId, "subscription_created",
            new { to = request.Tier, reason = request.Reason }, ct);
        return Ack($"{request.Tier} subscription created");
    }

    public override async Task<AckResponse> ChangeSubscriptionTier(SubscriptionRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await OpenAsync(ct);
        var oldTier = (string)(await ExecSpAsync(connection, "SELECT sp_change_subscription_tier(@t, @tier)", cmd =>
        {
            cmd.Parameters.AddWithValue("t", tenantsId);
            cmd.Parameters.AddWithValue("tier", request.Tier);
        }, ct))!;
        accessProvider.Invalidate(tenantsId);
        await AuditAsync(connection, "Billing", "Tenant", tenantsId, "subscription_tier_changed",
            new { from = oldTier, to = request.Tier, reason = request.Reason }, ct);
        return Ack($"Subscription changed from {oldTier} to {request.Tier}");
    }

    public override async Task<AckResponse> CancelSubscription(CancelSubscriptionRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_cancel_subscription(@t, @pe)", cmd =>
        {
            cmd.Parameters.AddWithValue("t", tenantsId);
            cmd.Parameters.AddWithValue("pe", request.AtPeriodEnd);
        }, ct);
        accessProvider.Invalidate(tenantsId);
        await AuditAsync(connection, "Billing", "Tenant", tenantsId, "subscription_canceled",
            new { at_period_end = request.AtPeriodEnd, reason = request.Reason }, ct);
        return Ack(request.AtPeriodEnd ? "Subscription will end at period end" : "Subscription canceled");
    }

    public override async Task<EventUpgradeList> ListEventUpgrades(PageRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var limit = request.Limit > 0 ? Math.Min(request.Limit, 200) : 50;
        var offset = Math.Max(request.Offset, 0);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%";
        await using var connection = await OpenAsync(ct);

        var response = new EventUpgradeList();
        await using (var countCmd = new NpgsqlCommand(
            "SELECT COUNT(*)::int FROM vw_event_upgrades WHERE (@q IS NULL OR event_title ILIKE @q OR tenant_name ILIKE @q)", connection))
        {
            countCmd.Parameters.Add(TextParam("q", search));
            response.Meta = new PageMeta
            {
                Total = (int)(await countCmd.ExecuteScalarAsync(ct))!,
                Offset = offset,
                Limit = limit
            };
        }

        await using var cmd = new NpgsqlCommand(
            "SELECT event_upgrades_id, events_id, event_title, event_status, tenants_id, tenant_name, tenant_slug, "
            + "tier, status, price_cents, sms_credits, custom_domain_limit, refunded_cents, created_at "
            + "FROM vw_event_upgrades WHERE (@q IS NULL OR event_title ILIKE @q OR tenant_name ILIKE @q) "
            + "ORDER BY created_at DESC OFFSET @o LIMIT @l", connection);
        cmd.Parameters.Add(TextParam("q", search));
        cmd.Parameters.AddWithValue("o", offset);
        cmd.Parameters.AddWithValue("l", limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Upgrades.Add(new EventUpgradeRow
            {
                EventUpgradesId = reader.GetGuid(0).ToString(),
                EventsId = reader.GetGuid(1).ToString(),
                EventTitle = reader.GetString(2),
                EventStatus = reader.GetString(3),
                TenantsId = reader.GetGuid(4).ToString(),
                TenantName = reader.GetString(5),
                TenantSlug = reader.GetString(6),
                Tier = reader.GetString(7),
                Status = reader.GetString(8),
                PriceCents = reader.GetInt32(9),
                SmsCredits = reader.GetInt32(10),
                CustomDomainLimit = reader.GetInt32(11),
                RefundedCents = reader.GetInt32(12),
                CreatedAtEpochSeconds = EpochOrZero(reader, 13)
            });
        }
        return response;
    }

    public override async Task<AckResponse> ActivateEventUpgrade(EventUpgradeRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var eventsId = Guid.Parse(request.EventsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_activate_event_upgrade(@e, @tier)", cmd =>
        {
            cmd.Parameters.AddWithValue("e", eventsId);
            cmd.Parameters.AddWithValue("tier", request.Tier);
        }, ct);
        await AuditAsync(connection, "Billing", "Event", eventsId, "pay_per_event_activated",
            new { tier = request.Tier, reason = request.Reason }, ct);
        return Ack($"Pay Per Event ({request.Tier}) activated");
    }

    public override async Task<AckResponse> CancelEventUpgrade(CancelEventUpgradeRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var eventsId = Guid.Parse(request.EventsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_cancel_event_upgrade(@e, @r)", cmd =>
        {
            cmd.Parameters.AddWithValue("e", eventsId);
            cmd.Parameters.AddWithValue("r", request.RefundCents);
        }, ct);
        await AuditAsync(connection, "Billing", "Event", eventsId, "pay_per_event_canceled",
            new { refund_cents = request.RefundCents, reason = request.Reason }, ct);
        return Ack("Pay Per Event canceled");
    }

    public override async Task<TenantAddonList> ListTenantAddons(TenantRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        await using var connection = await OpenAsync(ct);
        var response = new TenantAddonList();
        await using var cmd = new NpgsqlCommand(
            "SELECT tenant_addons_id, tenants_id, tenant_name, type, billing_period, quantity, price_cents, "
            + "setup_fee_cents, status, current_period_end, usage_count "
            + "FROM vw_tenant_addons WHERE (@t IS NULL OR tenants_id = @t) ORDER BY created_at DESC LIMIT 500", connection);
        cmd.Parameters.Add(new NpgsqlParameter("t", NpgsqlDbType.Uuid)
        {
            Value = string.IsNullOrEmpty(request.TenantsId) ? DBNull.Value : Guid.Parse(request.TenantsId)
        });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Addons.Add(new TenantAddonRow
            {
                TenantAddonsId = reader.GetGuid(0).ToString(),
                TenantsId = reader.GetGuid(1).ToString(),
                TenantName = reader.GetString(2),
                Type = reader.GetString(3),
                BillingPeriod = reader.GetString(4),
                Quantity = reader.GetInt32(5),
                PriceCents = reader.GetInt32(6),
                SetupFeeCents = reader.GetInt32(7),
                Status = reader.GetString(8),
                CurrentPeriodEndEpochSeconds = EpochOrZero(reader, 9),
                UsageCount = reader.GetInt32(10)
            });
        }
        return response;
    }

    public override async Task<AckResponse> ProvisionAddon(ProvisionAddonRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_provision_addon(@t, @type, @period, @qty)", cmd =>
        {
            cmd.Parameters.AddWithValue("t", tenantsId);
            cmd.Parameters.AddWithValue("type", request.Type);
            cmd.Parameters.AddWithValue("period", request.BillingPeriod);
            cmd.Parameters.AddWithValue("qty", Math.Max(request.Quantity, 1));
        }, ct);
        await AuditAsync(connection, "Billing", "Tenant", tenantsId, "addon_provisioned",
            new { type = request.Type, period = request.BillingPeriod, quantity = Math.Max(request.Quantity, 1), reason = request.Reason }, ct);
        return Ack($"{request.Type} add-on provisioned");
    }

    public override async Task<AckResponse> CancelAddon(CancelAddonRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var addonId = Guid.Parse(request.TenantAddonsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_cancel_addon(@id, @r)", cmd =>
        {
            cmd.Parameters.AddWithValue("id", addonId);
            cmd.Parameters.AddWithValue("r", request.RefundCents);
        }, ct);
        await AuditAsync(connection, "Billing", "TenantAddon", addonId, "addon_canceled",
            new { refund_cents = request.RefundCents, reason = request.Reason }, ct);
        return Ack("Add-on canceled");
    }

    public override async Task<FeeOverrideList> ListFeeOverrides(Empty request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        await using var connection = await OpenAsync(ct);
        var response = new FeeOverrideList();
        await using var cmd = new NpgsqlCommand(
            "SELECT scope, tenants_id, tenant_name, tenant_slug, events_id, event_title, percent_bps, flat_cents, "
            + "min_fee_cents, max_fee_cents, standard_percent_bps, standard_flat_cents, expires_at, updated_at "
            + "FROM vw_fee_overrides ORDER BY updated_at DESC LIMIT 500", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Overrides.Add(new FeeOverrideRow
            {
                Scope = reader.GetString(0),
                TenantsId = reader.GetGuid(1).ToString(),
                TenantName = reader.GetString(2),
                TenantSlug = reader.GetString(3),
                EventsId = reader.IsDBNull(4) ? "" : reader.GetGuid(4).ToString(),
                EventTitle = reader.IsDBNull(5) ? "" : reader.GetString(5),
                PercentBps = reader.GetInt32(6),
                FlatCents = reader.GetInt32(7),
                MinFeeCents = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                MaxFeeCents = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                StandardPercentBps = reader.GetInt32(10),
                StandardFlatCents = reader.GetInt32(11),
                ExpiresAtEpochSeconds = EpochOrZero(reader, 12),
                UpdatedAtEpochSeconds = EpochOrZero(reader, 13)
            });
        }
        return response;
    }

    public override async Task<AckResponse> SetEventFeeOverride(SetEventFeeOverrideRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        RequireReason(request.Reason);
        var ct = context.CancellationToken;
        var eventsId = Guid.Parse(request.EventsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_set_event_fee_override(@e, @bps, @flat, @min, @max, @exp)", cmd =>
        {
            cmd.Parameters.AddWithValue("e", eventsId);
            cmd.Parameters.AddWithValue("bps", request.PercentBps);
            cmd.Parameters.AddWithValue("flat", request.FlatCents);
            cmd.Parameters.Add(new NpgsqlParameter("min", NpgsqlDbType.Integer)
            { Value = request.MinFeeCents > 0 ? request.MinFeeCents : DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("max", NpgsqlDbType.Integer)
            { Value = request.MaxFeeCents > 0 ? request.MaxFeeCents : DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("exp", NpgsqlDbType.TimestampTz)
            {
                Value = request.ExpiresAtEpochSeconds > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(request.ExpiresAtEpochSeconds).UtcDateTime
                    : DBNull.Value
            });
        }, ct);
        await AuditAsync(connection, "FeeOverride", "Event", eventsId, "event_fee_override_set",
            new
            {
                percent_bps = request.PercentBps,
                flat_cents = request.FlatCents,
                min_fee_cents = request.MinFeeCents,
                max_fee_cents = request.MaxFeeCents,
                expires_at_epoch_seconds = request.ExpiresAtEpochSeconds,
                reason = request.Reason
            }, ct);
        return Ack("Event fee override applied");
    }

    public override async Task<AckResponse> ClearEventFeeOverride(ClearEventFeeOverrideRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        RequireReason(request.Reason);
        var ct = context.CancellationToken;
        var eventsId = Guid.Parse(request.EventsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_clear_event_fee_override(@e)",
            cmd => cmd.Parameters.AddWithValue("e", eventsId), ct);
        await AuditAsync(connection, "FeeOverride", "Event", eventsId, "event_fee_override_cleared",
            new { reason = request.Reason }, ct);
        return Ack("Event fee override cleared");
    }

    public override async Task<RevenueReport> GetRevenueReport(RevenueReportRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var (from, to) = Range(request.FromEpochSeconds, request.ToEpochSeconds);
        await using var connection = await OpenAsync(ct);
        var response = new RevenueReport
        {
            GeneratedAtEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await using (var cmd = RangeCmd(connection, "SELECT source, revenue_cents, item_count FROM sp_developer_revenue_by_source(@f, @t)", from, to))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.BySource.Add(new RevenueSourceRow
                {
                    Source = reader.GetString(0),
                    RevenueCents = reader.GetInt64(1),
                    ItemCount = reader.GetInt32(2)
                });
            }
        }

        await using (var cmd = RangeCmd(connection, "SELECT tier, service_fee_cents, billing_cents, tenant_count FROM sp_developer_revenue_by_tier(@f, @t)", from, to))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.ByTier.Add(new RevenueTierRow
                {
                    Tier = reader.GetString(0),
                    ServiceFeeCents = reader.GetInt64(1),
                    BillingCents = reader.GetInt64(2),
                    TenantCount = reader.GetInt32(3)
                });
            }
        }

        await using (var cmd = RangeCmd(connection, "SELECT bucket_start, service_fee_cents, billing_cents FROM sp_developer_revenue_timeseries(@f, @t)", from, to))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.Trend.Add(new RevenueTrendPoint
                {
                    BucketStartEpochSeconds = EpochOrZero(reader, 0),
                    ServiceFeeCents = reader.GetInt64(1),
                    BillingCents = reader.GetInt64(2)
                });
            }
        }
        return response;
    }

    public override async Task<TenantActivityList> GetTenantActivity(TenantActivityRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var (from, to) = Range(request.FromEpochSeconds, request.ToEpochSeconds);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%";
        await using var connection = await OpenAsync(ct);
        var response = new TenantActivityList();
        await using var cmd = new NpgsqlCommand(
            "SELECT tenants_id, name, slug, tier, events_created, tickets_sold, service_fee_cents, "
            + "billing_cents, avg_ticket_cents, subscription_status, total_count "
            + "FROM sp_developer_tenant_activity(@f, @t, @q, @tier, @o, @l)", connection);
        cmd.Parameters.AddWithValue("f", from);
        cmd.Parameters.AddWithValue("t", to);
        cmd.Parameters.Add(TextParam("q", search));
        cmd.Parameters.Add(TextParam("tier", string.IsNullOrWhiteSpace(request.Tier) ? null : request.Tier));
        cmd.Parameters.AddWithValue("o", Math.Max(request.Offset, 0));
        cmd.Parameters.AddWithValue("l", request.Limit > 0 ? Math.Min(request.Limit, 200) : 50);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Total = (int)reader.GetInt64(10);
            response.Rows.Add(new TenantActivityRow
            {
                TenantsId = reader.GetGuid(0).ToString(),
                Name = reader.GetString(1),
                Slug = reader.GetString(2),
                Tier = reader.GetString(3),
                EventsCreated = reader.GetInt32(4),
                TicketsSold = reader.GetInt32(5),
                ServiceFeeCents = reader.GetInt64(6),
                BillingCents = reader.GetInt64(7),
                AvgTicketCents = reader.GetInt64(8),
                SubscriptionStatus = reader.IsDBNull(9) ? "" : reader.GetString(9)
            });
        }
        return response;
    }

    private void RequireDeveloper()
    {
        if (!tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Developer access required"));
        }
    }

    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A reason is required for fee overrides"));
        }
    }

    private Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
        => db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);

    private static async Task<object?> ExecSpAsync(
        NpgsqlConnection connection, string sql, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        try
        {
            await using var cmd = new NpgsqlCommand(sql, connection);
            bind(cmd);
            return await cmd.ExecuteScalarAsync(ct);
        }
        catch (PostgresException exception) when (exception.SqlState is "P0001" or "P0002")
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, exception.MessageText));
        }
    }

    private async Task AuditAsync(
        NpgsqlConnection connection, string eventType, string subjectType, Guid subjectId,
        string action, object metadata, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_audit_log(@type, 'Developer', @actor, @stype, @subject, @action, @meta, NULL, NULL)", connection);
        cmd.Parameters.AddWithValue("type", eventType);
        cmd.Parameters.AddWithValue("actor", (object?)tenantContext.UsersId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("stype", subjectType);
        cmd.Parameters.AddWithValue("subject", subjectId);
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("meta", JsonSerializer.Serialize(metadata));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static NpgsqlParameter TextParam(string name, string? value)
        => new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    private static NpgsqlCommand RangeCmd(NpgsqlConnection connection, string sql, DateTime from, DateTime to)
    {
        var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("f", from);
        cmd.Parameters.AddWithValue("t", to);
        return cmd;
    }

    private static (DateTime From, DateTime To) Range(long fromEpoch, long toEpoch)
    {
        var to = toEpoch > 0 ? DateTimeOffset.FromUnixTimeSeconds(toEpoch).UtcDateTime : DateTime.UtcNow;
        var from = fromEpoch > 0 ? DateTimeOffset.FromUnixTimeSeconds(fromEpoch).UtcDateTime : to.AddMonths(-12);
        return (from, to);
    }

    private static long EpochOrZero(NpgsqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static AckResponse Ack(string message) => new() { Success = true, Message = message };
}
