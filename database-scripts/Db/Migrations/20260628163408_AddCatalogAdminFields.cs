using db.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAdminFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<bool>(
                name: "default_is_all_inclusive",
                table: "table_templates",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "sponsors",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "performers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "ix_table_templates_tenants_id_default_color",
                table: "table_templates",
                columns: new[] { "tenants_id", "default_color" },
                unique: true,
                filter: "default_color IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_table_templates_tenants_id_name",
                table: "table_templates",
                columns: new[] { "tenants_id", "name" },
                unique: true);

            // Reinstall views (now expose is_active / capacity / venue_type) and
            // stored procedures (new params: all-inclusive, is_active, address,
            // sp_link_venue_image) against the new columns. CREATE OR REPLACE.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_table_templates_tenants_id_default_color",
                table: "table_templates");

            migrationBuilder.DropIndex(
                name: "ix_table_templates_tenants_id_name",
                table: "table_templates");

            migrationBuilder.DropColumn(
                name: "capacity",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "venue_type",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "default_is_all_inclusive",
                table: "table_templates");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "sponsors");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "performers");
        }
    }
}
