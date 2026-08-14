using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class UpdateBookingLinesConstraint : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_booking_lines_Ref",
                table: "booking_lines");

            migrationBuilder.AddCheckConstraint(
                name: "CK_booking_lines_Ref",
                table: "booking_lines",
                sql: "(kind = 'Ticket' AND event_ticket_types_id IS NOT NULL AND tables_id IS NULL) OR (kind = 'Ticket' AND tables_id IS NOT NULL AND event_ticket_types_id IS NULL) OR (kind = 'Table' AND tables_id IS NOT NULL AND event_ticket_types_id IS NULL)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_booking_lines_Ref",
                table: "booking_lines");

            migrationBuilder.AddCheckConstraint(
                name: "CK_booking_lines_Ref",
                table: "booking_lines",
                sql: "(kind = 'Ticket' AND event_ticket_types_id IS NOT NULL AND tables_id IS NULL) OR (kind = 'Table' AND tables_id IS NOT NULL AND event_ticket_types_id IS NULL)");
        }
    }
}
