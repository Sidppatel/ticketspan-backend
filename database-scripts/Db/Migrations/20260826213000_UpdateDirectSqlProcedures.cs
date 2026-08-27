using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class UpdateDirectSqlProcedures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_link_event_image.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_link_venue_image.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_lock_table.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_release_table_lock.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_list_events_for_staff.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_search_events.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
