using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class AddTenantBrandColorRoles : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "brand_background",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "brand_button",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "brand_highlight",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "brand_text",
                table: "tenants",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "brand_background",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "brand_button",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "brand_highlight",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "brand_text",
                table: "tenants");
        }
    }
}
