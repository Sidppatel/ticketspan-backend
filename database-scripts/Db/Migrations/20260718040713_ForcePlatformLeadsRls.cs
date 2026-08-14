using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class ForcePlatformLeadsRls : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("rls_platform_leads.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE platform_leads NO FORCE ROW LEVEL SECURITY;");
        }
    }
}
