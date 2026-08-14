using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class FilterAdminDashboardStatsByTenant : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_admin_dashboard_stats CASCADE;");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
