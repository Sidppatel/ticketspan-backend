using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class UpdateVwTicketsVenueJoin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0117_v_tickets.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_check_in_ticket_by_token.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_lookup_booking_for_checkin.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
