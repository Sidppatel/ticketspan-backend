using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Svyne.Api.Services;

public sealed record TenantReportingAccessInfo(string Tier, bool OverrideEnabled, bool HasAdvanced);

public sealed class ReportingAccessProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private readonly IMemoryCache cache;

    public ReportingAccessProvider(IMemoryCache cache)
    {
        this.cache = cache;
    }

    public async Task<TenantReportingAccessInfo> GetAsync(NpgsqlConnection connection, Guid tenantsId, CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey(tenantsId), out TenantReportingAccessInfo? cached) && cached is not null)
        {
            return cached;
        }
        await using var cmd = new NpgsqlCommand(
            "SELECT tier, advanced_reporting_enabled, has_advanced_reporting FROM vw_tenant_reporting_access WHERE tenants_id = @t", connection);
        cmd.Parameters.AddWithValue("t", tenantsId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var info = await reader.ReadAsync(ct)
            ? new TenantReportingAccessInfo(reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2))
            : new TenantReportingAccessInfo("free", false, false);
        cache.Set(CacheKey(tenantsId), info, CacheTtl);
        return info;
    }

    public void Invalidate(Guid tenantsId)
    {
        cache.Remove(CacheKey(tenantsId));
    }

    private static string CacheKey(Guid tenantsId) => $"reporting-access:{tenantsId}";
}
