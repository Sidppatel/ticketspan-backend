using System.Text.Json;
using Grpc.Core;
using Npgsql;
using NpgsqlTypes;
using TicketSpan.Api.Data;
using TicketSpan.Api.Payments;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Billing;
using TicketSpan.Protos.Common;

namespace TicketSpan.Api.Services;

public sealed partial class DeveloperBillingServiceImpl
{
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
