using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class AddStaffEventAccessTimes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE staff_event_access ADD COLUMN IF NOT EXISTS access_start timestamp with time zone;");
            migrationBuilder.Sql("ALTER TABLE staff_event_access ADD COLUMN IF NOT EXISTS access_end timestamp with time zone;");

            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.security");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE staff_event_access DROP COLUMN IF EXISTS access_start;");
            migrationBuilder.Sql("ALTER TABLE staff_event_access DROP COLUMN IF EXISTS access_end;");
        }
    }
}
