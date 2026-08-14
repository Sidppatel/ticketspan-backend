using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class AddCheckInLogMethodStatusFailureReason : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "failure_reason",
                table: "checkin_logs",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "method",
                table: "checkin_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "qr_scan");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "checkin_logs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "success");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failure_reason",
                table: "checkin_logs");

            migrationBuilder.DropColumn(
                name: "method",
                table: "checkin_logs");

            migrationBuilder.DropColumn(
                name: "status",
                table: "checkin_logs");
        }
    }
}
