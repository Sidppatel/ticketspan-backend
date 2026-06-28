using db.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVenueCapacityAndType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");

            migrationBuilder.DropColumn(
                name: "capacity",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "venue_type",
                table: "venues");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "capacity",
                table: "venues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "venue_type",
                table: "venues",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
