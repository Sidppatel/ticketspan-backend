using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class UpdateVenueNotFoundCheck : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
