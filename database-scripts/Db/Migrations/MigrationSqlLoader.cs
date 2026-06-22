using Microsoft.EntityFrameworkCore.Migrations;

namespace db.Migrations;

internal static class MigrationSqlLoader
{
    internal static string Load(string fileName)
    {
        var asm = typeof(MigrationSqlLoader).Assembly;
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static void LoadAll(MigrationBuilder migrationBuilder, string folder)
    {
        var asm = typeof(MigrationSqlLoader).Assembly;
        var prefix = $"{asm.GetName().Name}.{folder}.";
        var names = asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            migrationBuilder.Sql(reader.ReadToEnd());
        }
    }
}
