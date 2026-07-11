using System.Text.Json;
using Grpc.Core;
using Npgsql;
using NpgsqlTypes;
using Svyne.Api.Data;
using Svyne.Api.Payments;
using Svyne.Api.Security;
using Svyne.Protos.Billing;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;







public sealed class DeveloperBillingServiceImpl : DeveloperBillingService.DeveloperBillingServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly ReportingAccessProvider accessProvider;
    private readonly SalesTaxService salesTax;

    public DeveloperBillingServiceImpl(Db db, TenantContext tenantContext, ReportingAccessProvider accessProvider,
        SalesTaxService salesTax)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.accessProvider = accessProvider;
        this.salesTax = salesTax;
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

    public override async Task<TaxReport> GetTaxReport(RevenueReportRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var (from, to) = Range(request.FromEpochSeconds, request.ToEpochSeconds);
        await using var connection = await OpenAsync(ct);
        var response = new TaxReport
        {
            GeneratedAtEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await using (var cmd = RangeCmd(connection, "SELECT events_id, event_title, tax_collected_cents, orders FROM sp_developer_tax_by_event(@f, @t)", from, to))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.ByEvent.Add(new TaxByEventRow
                {
                    EventsId = reader.GetGuid(0).ToString(),
                    EventTitle = reader.GetString(1),
                    TaxCollectedCents = reader.GetInt64(2),
                    Orders = reader.GetInt32(3)
                });
            }
        }

        await using (var cmd = RangeCmd(connection, "SELECT tenants_id, name, tax_collected_cents, orders FROM sp_developer_tax_by_tenant(@f, @t)", from, to))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.TotalTaxCents += reader.GetInt64(2);
                response.ByTenant.Add(new TaxByTenantRow
                {
                    TenantsId = reader.GetGuid(0).ToString(),
                    Name = reader.GetString(1),
                    TaxCollectedCents = reader.GetInt64(2),
                    Orders = reader.GetInt32(3)
                });
            }
        }

        await using (var cmd = RangeCmd(connection, "SELECT bucket_start, tax_collected_cents, orders FROM sp_developer_tax_by_month(@f, @t)", from, to))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.ByMonth.Add(new TaxByMonthRow
                {
                    BucketStartEpochSeconds = EpochOrZero(reader, 0),
                    TaxCollectedCents = reader.GetInt64(1),
                    Orders = reader.GetInt32(2)
                });
            }
        }

        await using (var cmd = RangeCmd(connection, "SELECT state, county, city, combined_rate, state_rate, county_rate, city_rate, local_rate, "
            + "tax_collected_cents, state_tax_cents, county_tax_cents, city_tax_cents, orders "
            + "FROM sp_developer_tax_by_jurisdiction(@f, @t)", from, to))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.ByJurisdiction.Add(new TaxByJurisdictionRow
                {
                    State = reader.GetString(0),
                    County = reader.GetString(1),
                    City = reader.GetString(2),
                    CombinedRate = reader.IsDBNull(3) ? 0 : (double)reader.GetDecimal(3),
                    StateRate = reader.IsDBNull(4) ? 0 : (double)reader.GetDecimal(4),
                    CountyRate = reader.IsDBNull(5) ? 0 : (double)reader.GetDecimal(5),
                    CityRate = reader.IsDBNull(6) ? 0 : (double)reader.GetDecimal(6),
                    LocalRate = reader.IsDBNull(7) ? 0 : (double)reader.GetDecimal(7),
                    TaxCollectedCents = reader.GetInt64(8),
                    StateTaxCents = reader.GetInt64(9),
                    CountyTaxCents = reader.GetInt64(10),
                    CityTaxCents = reader.GetInt64(11),
                    Orders = reader.GetInt32(12)
                });
            }
        }

        await using (var cmd = RangeCmd(connection, "SELECT combined_rate, state, tax_collected_cents, orders FROM sp_developer_tax_rate_summary(@f, @t)", from, to))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.RateSummary.Add(new TaxRateSummaryRow
                {
                    CombinedRate = reader.IsDBNull(0) ? 0 : (double)reader.GetDecimal(0),
                    State = reader.GetString(1),
                    TaxCollectedCents = reader.GetInt64(2),
                    Orders = reader.GetInt32(3)
                });
            }
        }

        return response;
    }

    public override async Task<TaxRemittanceReport> GetTaxRemittanceReport(RevenueReportRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var (from, to) = Range(request.FromEpochSeconds, request.ToEpochSeconds);
        await using var connection = await OpenAsync(ct);
        var response = new TaxRemittanceReport { GeneratedAtEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };

        await using var cmd = RangeCmd(connection,
            "SELECT collected_by, month_start, tenants_id, tenant_name, tax_cents, taxable_cents, orders "
            + "FROM sp_developer_tax_remittance(@f, @t)", from, to);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        TaxRemitMonthRow? month = null;
        string currentMode = "";
        while (await reader.ReadAsync(ct))
        {
            var mode = reader.GetString(0);
            var monthStart = EpochOrZero(reader, 1);
            if (month is null || mode != currentMode || month.MonthStartEpochSeconds != monthStart)
            {
                month = new TaxRemitMonthRow { MonthStartEpochSeconds = monthStart };
                currentMode = mode;
                (mode == "self" ? response.SelfMonths : response.PlatformMonths).Add(month);
            }
            var row = new TaxRemitTenantRow
            {
                TenantsId = reader.GetGuid(2).ToString(),
                TenantName = reader.GetString(3),
                TaxCents = reader.GetInt64(4),
                TaxableCents = reader.GetInt64(5),
                Orders = reader.GetInt32(6)
            };
            month.Tenants.Add(row);
            month.TaxCents += row.TaxCents;
            month.TaxableCents += row.TaxableCents;
            month.Orders += row.Orders;
            if (mode == "self")
            {
                response.SelfTotalCents += row.TaxCents;
            }
            else
            {
                response.PlatformTotalCents += row.TaxCents;
            }
        }
        return response;
    }

    public override async Task<TenantDashboard> GetTenantDashboard(TenantRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        var tenantsId = Guid.Parse(request.TenantsId);
        await using var connection = await OpenAsync(ct);
        var response = new TenantDashboard();

        await using (var cmd = TenantCmd(connection,
            "SELECT tier, total_revenue_cents, total_tax_cents, total_tickets_sold, event_count, "
            + "revenue_this_month_cents, revenue_last_month_cents, tax_this_month_cents, avg_ticket_cents "
            + "FROM sp_developer_tenant_stats(@tid)", tenantsId))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                response.Tier = reader.GetString(0);
                response.TotalRevenueCents = reader.GetInt64(1);
                response.TotalTaxCents = reader.GetInt64(2);
                response.TotalTicketsSold = reader.GetInt32(3);
                response.EventCount = reader.GetInt32(4);
                response.RevenueThisMonthCents = reader.GetInt64(5);
                response.RevenueLastMonthCents = reader.GetInt64(6);
                response.TaxThisMonthCents = reader.GetInt64(7);
                response.AvgTicketCents = reader.GetInt64(8);
            }
        }

        await using (var cmd = TenantCmd(connection,
            "SELECT events_id, event_title, start_date, venue_name, status, revenue_cents, "
            + "tickets_sold, capacity, tax_collected_cents FROM sp_developer_tenant_events(@tid)", tenantsId))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.Events.Add(new TenantDashboardEventRow
                {
                    EventsId = reader.GetGuid(0).ToString(),
                    EventTitle = reader.GetString(1),
                    StartDateEpochSeconds = EpochOrZero(reader, 2),
                    VenueName = reader.GetString(3),
                    Status = reader.GetString(4),
                    RevenueCents = reader.GetInt64(5),
                    TicketsSold = reader.GetInt32(6),
                    Capacity = reader.GetInt32(7),
                    TaxCollectedCents = reader.GetInt64(8)
                });
            }
        }

        await using (var cmd = TenantCmd(connection,
            "SELECT bucket_start, revenue_cents, tickets_sold FROM sp_developer_tenant_revenue_by_month(@tid)", tenantsId))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.RevenueByMonth.Add(new TenantRevenueMonthRow
                {
                    BucketStartEpochSeconds = EpochOrZero(reader, 0),
                    RevenueCents = reader.GetInt64(1),
                    TicketsSold = reader.GetInt32(2)
                });
            }
        }

        await using (var cmd = TenantCmd(connection,
            "SELECT venue_name, state, tax_collected_cents, orders FROM sp_developer_tenant_tax_by_venue(@tid)", tenantsId))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.TaxByVenue.Add(new TenantTaxByVenueRow
                {
                    VenueName = reader.GetString(0),
                    State = reader.GetString(1),
                    TaxCollectedCents = reader.GetInt64(2),
                    Orders = reader.GetInt32(3)
                });
            }
        }

        return response;
    }

    public override async Task<TaxOverrideList> ListTaxOverrides(Empty request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        await using var connection = await OpenAsync(ct);
        var response = new TaxOverrideList();
        await using var cmd = new NpgsqlCommand(
            "SELECT events_id, event_title, tenant_name, tax_exempt, tax_rate_override, updated_at "
            + "FROM sp_list_event_tax_overrides()", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Overrides.Add(new TaxOverrideRow
            {
                EventsId = reader.GetGuid(0).ToString(),
                EventTitle = reader.GetString(1),
                TenantName = reader.GetString(2),
                TaxExempt = reader.GetBoolean(3),
                RateBps = reader.IsDBNull(4) ? 0 : (int)Math.Round(reader.GetDecimal(4) * 10000m),
                UpdatedAtEpochSeconds = EpochOrZero(reader, 5)
            });
        }
        return response;
    }

    public override async Task<AckResponse> SetEventTaxOverride(SetEventTaxOverrideRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        RequireReason(request.Reason);
        if (!request.TaxExempt && (request.RateBps < 0 || request.RateBps > 5000))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Tax rate must be between 0% and 50%"));
        }
        var ct = context.CancellationToken;
        var eventsId = Guid.Parse(request.EventsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_set_event_tax_override(@e, @ex, @rate)", cmd =>
        {
            cmd.Parameters.AddWithValue("e", eventsId);
            cmd.Parameters.AddWithValue("ex", request.TaxExempt);
            cmd.Parameters.AddWithValue("rate", request.RateBps / 10000m);
        }, ct);
        await AuditAsync(connection, "TaxOverride", "Event", eventsId, "event_tax_override_set",
            new { tax_exempt = request.TaxExempt, rate_bps = request.RateBps, reason = request.Reason }, ct);
        return Ack(request.TaxExempt ? "Event marked tax exempt" : "Event tax rate overridden");
    }

    public override async Task<TaxRateList> ListTaxRates(Empty request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        await using var connection = await OpenAsync(ct);
        var response = new TaxRateList();
        await using var cmd = new NpgsqlCommand(
            "SELECT zip_code, city, state, county, combined_rate, state_rate, county_rate, city_rate, "
            + "local_rate, api_response_id, fetched_at FROM vw_tax_rate_cache ORDER BY zip_code", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Rates.Add(new TaxRateRow
            {
                ZipCode = reader.GetString(0),
                City = reader.IsDBNull(1) ? "" : reader.GetString(1),
                State = reader.IsDBNull(2) ? "" : reader.GetString(2),
                County = reader.IsDBNull(3) ? "" : reader.GetString(3),
                CombinedRate = (double)reader.GetDecimal(4),
                StateRate = (double)reader.GetDecimal(5),
                CountyRate = (double)reader.GetDecimal(6),
                CityRate = (double)reader.GetDecimal(7),
                LocalRate = (double)reader.GetDecimal(8),
                SourceRef = reader.IsDBNull(9) ? "" : reader.GetString(9),
                FetchedAtEpochSeconds = EpochOrZero(reader, 10)
            });
        }
        return response;
    }

    public override async Task<TaxRateRow> LookupTaxRate(TaxRateLookupRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        var zip = request.Zip?.Trim() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(zip, @"^\d{5}$"))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Enter a valid 5-digit US zip code"));
        }
        var ct = context.CancellationToken;
        await using var connection = await OpenAsync(ct);
        await salesTax.EnsureRateForZipAsync(connection, zip, ct, force: true);
        await using var cmd = new NpgsqlCommand(
            "SELECT zip_code, city, state, county, combined_rate, state_rate, county_rate, city_rate, "
            + "local_rate, api_response_id, fetched_at FROM vw_tax_rate_cache WHERE zip_code = @z", connection);
        cmd.Parameters.AddWithValue("z", zip);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"SalesTaxZip has no rate for zip {zip}"));
        }
        return new TaxRateRow
        {
            ZipCode = reader.GetString(0),
            City = reader.IsDBNull(1) ? "" : reader.GetString(1),
            State = reader.IsDBNull(2) ? "" : reader.GetString(2),
            County = reader.IsDBNull(3) ? "" : reader.GetString(3),
            CombinedRate = (double)reader.GetDecimal(4),
            StateRate = (double)reader.GetDecimal(5),
            CountyRate = (double)reader.GetDecimal(6),
            CityRate = (double)reader.GetDecimal(7),
            LocalRate = (double)reader.GetDecimal(8),
            SourceRef = reader.IsDBNull(9) ? "" : reader.GetString(9),
            FetchedAtEpochSeconds = EpochOrZero(reader, 10)
        };
    }

    public override async Task<AckResponse> RefreshAllTaxRates(Empty request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        await using var connection = await OpenAsync(ct);
        var zips = new List<string>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT zip_code FROM vw_tax_rate_cache "
            + "UNION SELECT zip_code FROM sp_list_venue_tax_summaries() WHERE zip_code <> ''", connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                zips.Add(reader.GetString(0));
            }
        }
        foreach (var zip in zips)
        {
            await salesTax.EnsureRateForZipAsync(connection, zip, ct, force: true);
        }
        return Ack($"Refreshed {zips.Count} tax rates");
    }

    public override async Task<VenueTaxSummaryList> ListVenueTaxSummaries(Empty request, ServerCallContext context)
    {
        RequireDeveloper();
        var ct = context.CancellationToken;
        await using var connection = await OpenAsync(ct);
        var response = new VenueTaxSummaryList();
        await using var cmd = new NpgsqlCommand(
            "SELECT venues_id, venue_name, tenant_name, city, state, zip_code, combined_rate, "
            + "state_rate, county_rate, city_rate, local_rate, fetched_at FROM sp_list_venue_tax_summaries()", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Venues.Add(new VenueTaxSummaryRow
            {
                VenuesId = reader.GetGuid(0).ToString(),
                VenueName = reader.GetString(1),
                TenantName = reader.GetString(2),
                City = reader.GetString(3),
                State = reader.GetString(4),
                ZipCode = reader.GetString(5),
                CombinedRate = (double)reader.GetDecimal(6),
                StateRate = (double)reader.GetDecimal(7),
                CountyRate = (double)reader.GetDecimal(8),
                CityRate = (double)reader.GetDecimal(9),
                LocalRate = (double)reader.GetDecimal(10),
                FetchedAtEpochSeconds = EpochOrZero(reader, 11)
            });
        }
        return response;
    }

    public override async Task<AckResponse> ClearEventTaxOverride(ClearEventFeeOverrideRequest request, ServerCallContext context)
    {
        RequireDeveloper();
        RequireReason(request.Reason);
        var ct = context.CancellationToken;
        var eventsId = Guid.Parse(request.EventsId);
        await using var connection = await OpenAsync(ct);
        await ExecSpAsync(connection, "SELECT sp_clear_event_tax_override(@e)",
            cmd => cmd.Parameters.AddWithValue("e", eventsId), ct);
        await AuditAsync(connection, "TaxOverride", "Event", eventsId, "event_tax_override_cleared",
            new { reason = request.Reason }, ct);
        return Ack("Event tax override cleared");
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

    private static NpgsqlCommand TenantCmd(NpgsqlConnection connection, string sql, Guid tenantsId)
    {
        var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("tid", tenantsId);
        return cmd;
    }

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
