using System.Reflection;
using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Admin;
using TicketSpan.Protos.Common;

namespace TicketSpan.Api.Services;

public sealed class MaintenanceServiceImpl : MaintenanceService.MaintenanceServiceBase
{
    private static readonly string[] SqlFolders =
    {
        "functions", "views", "stored_procedures", "policies", "security"
    };

    private readonly Db db;
    private readonly TenantContext tenantContext;
    private readonly ILogger<MaintenanceServiceImpl> logger;

    public MaintenanceServiceImpl(Db db, TenantContext tenantContext, ILogger<MaintenanceServiceImpl> logger)
    {
        this.db = db;
        this.tenantContext = tenantContext;
        this.logger = logger;
    }

    public override async Task<ReloadSqlResult> ReloadSqlObjects(Empty request, ServerCallContext context)
    {
        if (!tenantContext.IsDeveloper)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Developer access required"));
        }

        var ct = context.CancellationToken;
        var asm = typeof(MaintenanceServiceImpl).Assembly;
        var result = new ReloadSqlResult();

        await using var connection = await db.OpenBootstrapAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        await using (var dropViews = new NpgsqlCommand(@"
            DO $$
            DECLARE r RECORD;
            BEGIN
                FOR r IN (SELECT viewname FROM pg_views WHERE schemaname = 'public' AND viewname LIKE 'vw_%') LOOP
                    EXECUTE 'DROP VIEW IF EXISTS ' || quote_ident(r.viewname) || ' CASCADE';
                END LOOP;
            END $$;", connection, tx))
        {
            await dropViews.ExecuteNonQueryAsync(ct);
        }

        foreach (var folder in SqlFolders)
        {
            var prefix = $"{asm.GetName().Name}.Sql.{folder}.";
            var names = asm.GetManifestResourceNames()
                .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                            && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                await using var stream = asm.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                var sql = await reader.ReadToEndAsync(ct);
                await using var cmd = new NpgsqlCommand(sql, connection, tx);
                await cmd.ExecuteNonQueryAsync(ct);
                result.Files.Add(name[prefix.Length..]);
            }
        }

        await tx.CommitAsync(ct);
        result.FilesApplied = result.Files.Count;
        logger.LogInformation("SQL objects reloaded by developer {UserId}: {Count} files", tenantContext.UsersId, result.FilesApplied);
        return result;
    }
}
