using System.Collections.Concurrent;
using Npgsql;

namespace TicketSpan.Api.Data;

public sealed class AppSettingsProvider
{
    private readonly Db db;
    private readonly ConcurrentDictionary<string, string> cache = new();
    private volatile bool loaded;

    public AppSettingsProvider(Db db)
    {
        this.db = db;
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (loaded)
        {
            return;
        }
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand("SELECT key, value FROM vw_app_settings", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            cache[reader.GetString(0)] = reader.GetString(1);
        }
        loaded = true;
    }

    public async Task<string> GetStringAsync(string key, string fallback, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        return cache.TryGetValue(key, out var value) ? value : fallback;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await GetStringAsync(key, string.Empty, ct);
        return int.TryParse(raw, out var parsed) ? parsed : fallback;
    }
}
