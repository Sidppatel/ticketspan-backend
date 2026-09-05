using Npgsql;
using TicketSpan.Api.Data;

namespace TicketSpan.Api.Services;

public record PruneResult(int RowsDeleted);

public record TableCleanupSummary(
    int OpenIddictTokensDeleted,
    int OpenIddictAuthorizationsDeleted,
    int AuditLogsDeleted
);

public interface ITableCleanupService
{
    Task<PruneResult> PruneOpenIddictTokensAsync(DateTimeOffset? olderThan = null, CancellationToken ct = default);
    Task<PruneResult> PruneOpenIddictAuthorizationsAsync(DateTimeOffset? olderThan = null, CancellationToken ct = default);
    Task<PruneResult> PruneAuditLogsAsync(DateTimeOffset? olderThan = null, bool onlyResolved = false, CancellationToken ct = default);
    Task<TableCleanupSummary> RunFullCleanupAsync(CancellationToken ct = default);
}

public sealed class TableCleanupService : ITableCleanupService
{
    private readonly Db db;
    private readonly ILogger<TableCleanupService> logger;

    public TableCleanupService(Db db, ILogger<TableCleanupService> logger)
    {
        this.db = db;
        this.logger = logger;
    }

    public async Task<PruneResult> PruneOpenIddictTokensAsync(DateTimeOffset? olderThan = null, CancellationToken ct = default)
    {
        await using var connection = await db.OpenBootstrapAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_prune_openiddict_tokens(@older_than)", connection);
        cmd.Parameters.AddWithValue("older_than", (object?)olderThan ?? DBNull.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        var deleted = Convert.ToInt32(result ?? 0);
        logger.LogInformation("Pruned {Count} OpenIddict tokens", deleted);
        return new PruneResult(deleted);
    }

    public async Task<PruneResult> PruneOpenIddictAuthorizationsAsync(DateTimeOffset? olderThan = null, CancellationToken ct = default)
    {
        await using var connection = await db.OpenBootstrapAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_prune_openiddict_authorizations(@older_than)", connection);
        cmd.Parameters.AddWithValue("older_than", (object?)olderThan ?? DBNull.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        var deleted = Convert.ToInt32(result ?? 0);
        logger.LogInformation("Pruned {Count} OpenIddict authorizations", deleted);
        return new PruneResult(deleted);
    }

    public async Task<PruneResult> PruneAuditLogsAsync(DateTimeOffset? olderThan = null, bool onlyResolved = false, CancellationToken ct = default)
    {
        await using var connection = await db.OpenBootstrapAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_prune_audit_logs(@older_than, @only_resolved)", connection);
        cmd.Parameters.AddWithValue("older_than", (object?)olderThan ?? DBNull.Value);
        cmd.Parameters.AddWithValue("only_resolved", onlyResolved);
        var result = await cmd.ExecuteScalarAsync(ct);
        var deleted = Convert.ToInt32(result ?? 0);
        logger.LogInformation("Pruned {Count} audit log entries", deleted);
        return new PruneResult(deleted);
    }

    public async Task<TableCleanupSummary> RunFullCleanupAsync(CancellationToken ct = default)
    {
        var tokensRes = await PruneOpenIddictTokensAsync(null, ct);
        var authsRes = await PruneOpenIddictAuthorizationsAsync(null, ct);
        var logsRes = await PruneAuditLogsAsync(null, false, ct);
        return new TableCleanupSummary(tokensRes.RowsDeleted, authsRes.RowsDeleted, logsRes.RowsDeleted);
    }
}
