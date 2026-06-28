using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "schedule_items",
                columns: table => new
                {
                    schedule_items_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    type_category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_items", x => x.schedule_items_id);
                    table.CheckConstraint("CK_schedule_items_TimeRange", "end_time > start_time");
                    table.CheckConstraint("CK_schedule_items_TypeCategory", "type_category IN ('Performance','Break','Intermission','DJ Set','Networking','Other')");
                    table.ForeignKey(
                        name: "fk_schedule_items_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_schedule_items_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_schedule_items_events_id_start_time",
                table: "schedule_items",
                columns: new[] { "events_id", "start_time" });

            migrationBuilder.CreateIndex(
                name: "ix_schedule_items_tenants_id",
                table: "schedule_items",
                column: "tenants_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "schedule_items");
        }
    }
}
