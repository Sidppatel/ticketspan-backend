using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEventCategoryConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_events_Category",
                table: "events");

            migrationBuilder.Sql("DELETE FROM enum_definitions WHERE enum_type = 'EventCategory';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_events_Category",
                table: "events",
                sql: "category IS NULL OR category IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports')");
        }
    }
}
