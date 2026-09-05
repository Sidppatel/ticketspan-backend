using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class AddTenantColumnsToVwEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0131_02_v_events_with_sponsors.sql"));
            migrationBuilder.Sql("ALTER VIEW vw_events SET (security_invoker = true);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
