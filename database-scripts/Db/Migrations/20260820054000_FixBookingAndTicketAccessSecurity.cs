using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class FixBookingAndTicketAccessSecurity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("00_app_security_functions.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("rls_purchases.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("rls_booking_lines.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_set_ticket_invite.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_revoke_ticket_invite.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_claim_ticket_self.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
