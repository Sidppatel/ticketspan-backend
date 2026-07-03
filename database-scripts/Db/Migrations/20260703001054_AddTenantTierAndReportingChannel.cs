using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantTierAndReportingChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "advanced_reporting_enabled",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "tier",
                table: "tenants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "free");

            migrationBuilder.AddColumn<string>(
                name: "sales_channel",
                table: "bookings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "direct");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "advanced_reporting_enabled",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tier",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "sales_channel",
                table: "bookings");
        }
    }
}
