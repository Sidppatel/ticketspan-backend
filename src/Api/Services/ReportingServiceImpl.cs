using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Common;
using TicketSpan.Protos.Reporting;

namespace TicketSpan.Api.Services;

public sealed class ReportingServiceImpl : ReportingService.ReportingServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly ReportingAccessProvider accessProvider;

    public ReportingServiceImpl(Db db, TenantContext tenantContext, ReportingAccessProvider accessProvider)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.accessProvider = accessProvider;
    }

    public override async Task<ReportingAccess> GetReportingAccess(Empty request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tenantsId = RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantsId, ct);
        var access = await accessProvider.GetAsync(connection, tenantsId, ct);
        return new ReportingAccess
        {
            Tier = access.Tier,
            HasAdvancedReporting = access.HasAdvanced,
            AdvancedReportingOverride = access.OverrideEnabled,
            TaxCollectionMode = access.TaxCollectionMode
        };
    }

    public override async Task<ReportSummary> GetReportSummary(ReportRangeRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tenantsId = RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantsId, ct);
        var access = await accessProvider.GetAsync(connection, tenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT revenue_cents, orders, tickets_sold, average_order_cents, visits, conversion_bps, refunded_cents, refunded_orders "
            + "FROM sp_report_summary(@f, @t)", connection);
        AddRange(cmd, request.FromEpochSeconds, request.ToEpochSeconds);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var summary = new ReportSummary { GeneratedAtEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        if (await reader.ReadAsync(ct))
        {
            summary.RevenueCents = reader.GetInt64(0);
            summary.Orders = reader.GetInt32(1);
            summary.TicketsSold = reader.GetInt32(2);
            summary.AverageOrderCents = reader.GetInt64(3);
            summary.Visits = reader.GetInt32(4);
            summary.ConversionBps = reader.GetInt32(5);
            summary.RefundedCents = access.HasAdvanced ? reader.GetInt64(6) : 0;
            summary.RefundedOrders = access.HasAdvanced ? reader.GetInt32(7) : 0;
        }
        return summary;
    }

    public override async Task<RevenueTimeseries> GetRevenueTimeseries(TimeseriesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tenantsId = RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantsId, ct);
        var access = await accessProvider.GetAsync(connection, tenantsId, ct);
        if (request.Bucket == "year" && !access.HasAdvanced)
        {
            await LogAccessAttemptAsync(connection, tenantsId, "yearly_timeseries_denied", ct);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Advanced reporting required for yearly view"));
        }
        await using var cmd = new NpgsqlCommand(
            "SELECT bucket_start, revenue_cents, orders, tickets_sold FROM sp_report_revenue_timeseries(@f, @t, @b)", connection);
        AddRange(cmd, request.FromEpochSeconds, request.ToEpochSeconds);
        cmd.Parameters.AddWithValue("b", request.Bucket);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var response = new RevenueTimeseries { GeneratedAtEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        while (await reader.ReadAsync(ct))
        {
            response.Points.Add(new RevenueTimeseriesPoint
            {
                BucketStartEpochSeconds = new DateTimeOffset(reader.GetDateTime(0), TimeSpan.Zero).ToUnixTimeSeconds(),
                RevenueCents = reader.GetInt64(1),
                Orders = reader.GetInt32(2),
                TicketsSold = reader.GetInt32(3)
            });
        }
        return response;
    }

    public override async Task<EventPerformanceList> GetEventPerformance(ReportRangeRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tenantsId = RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantsId, ct);
        var access = await accessProvider.GetAsync(connection, tenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT events_id, event_title, event_start_date, event_status, revenue_cents, orders, tickets_sold, checked_in, "
            + "capacity, capacity_used_bps, attendance_rate_bps, revenue_per_attendee_cents, refunded_cents, refunded_orders, sales_per_day_milli "
            + "FROM sp_report_event_performance(@f, @t)", connection);
        AddRange(cmd, request.FromEpochSeconds, request.ToEpochSeconds);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var response = new EventPerformanceList { GeneratedAtEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        while (await reader.ReadAsync(ct))
        {
            response.Rows.Add(new EventPerformanceRow
            {
                EventsId = reader.GetGuid(0).ToString(),
                EventTitle = reader.GetString(1),
                EventStartEpochSeconds = new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero).ToUnixTimeSeconds(),
                EventStatus = reader.GetString(3),
                RevenueCents = reader.GetInt64(4),
                Orders = reader.GetInt32(5),
                TicketsSold = reader.GetInt32(6),
                CheckedIn = reader.GetInt32(7),
                Capacity = access.HasAdvanced ? reader.GetInt32(8) : 0,
                CapacityUsedBps = access.HasAdvanced ? reader.GetInt32(9) : 0,
                AttendanceRateBps = reader.GetInt32(10),
                RevenuePerAttendeeCents = access.HasAdvanced ? reader.GetInt64(11) : 0,
                RefundedCents = access.HasAdvanced ? reader.GetInt64(12) : 0,
                RefundedOrders = access.HasAdvanced ? reader.GetInt32(13) : 0,
                SalesPerDayMilli = access.HasAdvanced ? reader.GetInt32(14) : 0
            });
        }
        return response;
    }

    public override async Task<TicketTypeBreakdownList> GetTicketTypeBreakdown(ReportRangeRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tenantsId = RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantsId, ct);
        var access = await accessProvider.GetAsync(connection, tenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT event_ticket_types_id, label, events_id, event_title, price_cents, quantity_sold, revenue_cents, refunded_quantity, refunded_cents "
            + "FROM sp_report_ticket_type_breakdown(@f, @t)", connection);
        AddRange(cmd, request.FromEpochSeconds, request.ToEpochSeconds);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var response = new TicketTypeBreakdownList { GeneratedAtEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        while (await reader.ReadAsync(ct))
        {
            response.Rows.Add(new TicketTypeBreakdownRow
            {
                EventTicketTypesId = reader.IsDBNull(0) ? string.Empty : reader.GetGuid(0).ToString(),
                Label = reader.GetString(1),
                EventsId = reader.GetGuid(2).ToString(),
                EventTitle = reader.GetString(3),
                PriceCents = reader.GetInt64(4),
                QuantitySold = reader.GetInt32(5),
                RevenueCents = reader.GetInt64(6),
                RefundedQuantity = access.HasAdvanced ? reader.GetInt32(7) : 0,
                RefundedCents = access.HasAdvanced ? reader.GetInt64(8) : 0
            });
        }
        return response;
    }

    public override async Task<SalesByChannelList> GetSalesByChannel(ReportRangeRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tenantsId = RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantsId, ct);
        var access = await accessProvider.GetAsync(connection, tenantsId, ct);
        if (!access.HasAdvanced)
        {
            await LogAccessAttemptAsync(connection, tenantsId, "sales_by_channel_denied", ct);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Advanced reporting required"));
        }
        await LogAccessAttemptAsync(connection, tenantsId, "sales_by_channel_granted", ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT channel, orders, tickets_sold, revenue_cents FROM sp_report_sales_by_channel(@f, @t)", connection);
        AddRange(cmd, request.FromEpochSeconds, request.ToEpochSeconds);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var response = new SalesByChannelList { GeneratedAtEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        while (await reader.ReadAsync(ct))
        {
            response.Rows.Add(new SalesByChannelRow
            {
                Channel = reader.GetString(0),
                Orders = reader.GetInt32(1),
                TicketsSold = reader.GetInt32(2),
                RevenueCents = reader.GetInt64(3)
            });
        }
        return response;
    }

    public override async Task<TaxReport> GetTaxReport(ReportRangeRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tenantsId = RequireTenant();
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantsId, ct);
        var access = await accessProvider.GetAsync(connection, tenantsId, ct);
        if (access.TaxCollectionMode != "self")
        {
            await LogAccessAttemptAsync(connection, tenantsId, "tax_report_denied", ct);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Tax reporting is only available when your organization collects its own sales tax"));
        }
        await using var cmd = new NpgsqlCommand(
            "SELECT month_start, events_id, event_title, tax_cents, taxable_cents, orders "
            + "FROM sp_report_tax_by_month_event(@f, @t)", connection);
        AddRange(cmd, request.FromEpochSeconds, request.ToEpochSeconds);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var response = new TaxReport { GeneratedAtEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        TaxMonthRow? month = null;
        while (await reader.ReadAsync(ct))
        {
            var monthStart = new DateTimeOffset(reader.GetDateTime(0), TimeSpan.Zero).ToUnixTimeSeconds();
            if (month is null || month.MonthStartEpochSeconds != monthStart)
            {
                month = new TaxMonthRow { MonthStartEpochSeconds = monthStart };
                response.Months.Add(month);
            }
            var row = new TaxEventRow
            {
                EventsId = reader.GetGuid(1).ToString(),
                EventTitle = reader.GetString(2),
                TaxCents = reader.GetInt64(3),
                TaxableCents = reader.GetInt64(4),
                Orders = reader.GetInt32(5)
            };
            month.Events.Add(row);
            month.TaxCents += row.TaxCents;
            month.TaxableCents += row.TaxableCents;
            month.Orders += row.Orders;
        }
        return response;
    }

    private Guid RequireTenant()
    {
        if (tenantContext.TenantsId is { } tenantsId)
        {
            return tenantsId;
        }
        throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context required"));
    }

    private async Task LogAccessAttemptAsync(NpgsqlConnection connection, Guid tenantsId, string action, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_audit_log('ReportingAccess', 'Admin', @actor, 'Tenant', @subject, @action, NULL, NULL, NULL)", connection);
        cmd.Parameters.AddWithValue("actor", (object?)tenantContext.UsersId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subject", tenantsId);
        cmd.Parameters.AddWithValue("action", action);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddRange(NpgsqlCommand cmd, long fromEpochSeconds, long toEpochSeconds)
    {
        cmd.Parameters.AddWithValue("f", DateTimeOffset.FromUnixTimeSeconds(fromEpochSeconds).UtcDateTime);
        cmd.Parameters.AddWithValue("t", DateTimeOffset.FromUnixTimeSeconds(toEpochSeconds).UtcDateTime);
    }
}
