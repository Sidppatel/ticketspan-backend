using Microsoft.EntityFrameworkCore.Migrations;
using db.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddEventFeesIncluded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "fees_included",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Re-install SQL artifacts so vw_events (now exposing fees_included) and
            // sp_set_event_fees_included are (re)created. Idempotent.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fees_included",
                table: "events");
        }
    }
}
