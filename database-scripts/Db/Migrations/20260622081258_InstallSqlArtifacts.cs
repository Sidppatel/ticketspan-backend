using Microsoft.EntityFrameworkCore.Migrations;
using db.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class InstallSqlArtifacts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
