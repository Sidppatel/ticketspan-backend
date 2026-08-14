using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class AddPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0201_performance_indexes.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_events_tenant_status_start;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_bookings_events_status_created;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_bookings_users_status_created;");
        }
    }
}
