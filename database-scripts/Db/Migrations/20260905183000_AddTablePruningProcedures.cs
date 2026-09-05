using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class AddTablePruningProcedures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_prune_openiddict_tokens.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_prune_openiddict_authorizations.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_prune_audit_logs.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
