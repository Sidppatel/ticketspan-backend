using Db;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Supabase exposes two pooler endpoints: transaction (port 6543) for runtime
// queries and session (port 5432) for DDL. EF migrations need session.
// MIGRATION_DATABASE_URL takes precedence so backend's runtime DATABASE_URL
// (transaction pooler) doesn't have to change. Fall back to DATABASE_URL when
// MIGRATION_DATABASE_URL is unset (e.g. local Supabase where one URL works
// for both).
var migrationUrl = Environment.GetEnvironmentVariable("MIGRATION_DATABASE_URL");
if (!string.IsNullOrEmpty(migrationUrl))
{
    Environment.SetEnvironmentVariable("DATABASE_URL", migrationUrl);
}

var factory = new DesignTimeDbContextFactory();
const int maxRetries = 8;

for (var attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        await using var ctx = factory.CreateDbContext(args);
        Console.WriteLine($"[migrate] checking state (attempt {attempt})");

        var pending = (await ctx.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            Console.WriteLine("[migrate] up to date; nothing to apply");
            return 0;
        }

        var schemaPreExists = await TableExistsAsync(ctx, "addresses");
        var historyExists = await TableExistsAsync(ctx, "__EFMigrationsHistory");

        if (schemaPreExists && !historyExists)
        {
            Console.WriteLine("[migrate] schema pre-applied externally (Supabase pull?). Bootstrapping __EFMigrationsHistory.");
            await BootstrapHistoryAsync(ctx);
            pending = (await ctx.Database.GetPendingMigrationsAsync()).ToList();
        }
        else if (schemaPreExists && historyExists && pending.Count == ctx.Database.GetMigrations().Count())
        {
            Console.WriteLine("[migrate] schema pre-applied but history empty. Backfilling __EFMigrationsHistory.");
            await BackfillHistoryAsync(ctx);
            pending = (await ctx.Database.GetPendingMigrationsAsync()).ToList();
        }

        if (pending.Count == 0)
        {
            Console.WriteLine("[migrate] up to date after bootstrap");
            return 0;
        }

        Console.WriteLine($"[migrate] applying {pending.Count} pending: {string.Join(", ", pending)}");
        await ctx.Database.MigrateAsync();
        Console.WriteLine("[migrate] done");
        return 0;
    }
    catch (Exception ex) when (IsTransient(ex) && attempt < maxRetries)
    {
        var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attempt, 5)));
        Console.Error.WriteLine($"[migrate] db not ready: {ex.GetBaseException().Message}; retry in {delay.TotalSeconds}s");
        await Task.Delay(delay);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[migrate] failed: {ex}");
        return 1;
    }
}

Console.Error.WriteLine("[migrate] exhausted retries");
return 1;

static async Task<bool> TableExistsAsync(EventPlatformDbContext ctx, string tableName)
{
    var conn = ctx.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @t)";
    var p = cmd.CreateParameter();
    p.ParameterName = "t";
    p.Value = tableName;
    cmd.Parameters.Add(p);
    var result = await cmd.ExecuteScalarAsync();
    return result is bool b && b;
}

static async Task BootstrapHistoryAsync(EventPlatformDbContext ctx)
{
    await ctx.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
            "MigrationId" varchar(150) NOT NULL,
            "ProductVersion" varchar(32) NOT NULL,
            CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
        )
        """);
    await BackfillHistoryAsync(ctx);
}

static async Task BackfillHistoryAsync(EventPlatformDbContext ctx)
{
    var migrations = ctx.Database.GetMigrations().ToList();
    var backfillMigrations = migrations.Where(id => string.Compare(id, "20260428000000", StringComparison.Ordinal) < 0).ToList();
    
    foreach (var id in backfillMigrations)
    {
        await ctx.Database.ExecuteSqlRawAsync(
            """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ({0}, '10.0.7') ON CONFLICT ("MigrationId") DO NOTHING""",
            id);
    }
    Console.WriteLine($"[migrate] history populated with {backfillMigrations.Count} base migrations");
}

static bool IsTransient(Exception ex)
{
    for (var current = (Exception?)ex; current is not null; current = current.InnerException)
    {
        if (current is PostgresException pg)
        {
            return pg.SqlState is "57P03" or "08006" or "08001" or "08000";
        }
        if (current is TimeoutException || current is System.IO.IOException)
            return true;
        if (current is NpgsqlException && current is not PostgresException)
            return true;
    }
    return false;
}
