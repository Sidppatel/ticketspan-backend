using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class UpdateVwTicketsVenueJoin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0117_v_tickets.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
