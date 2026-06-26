using System;
using db.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddEventTypeAndPriceRuleScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "prices_id",
                table: "price_rules",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "events_id",
                table: "price_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "price_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Price");

            migrationBuilder.AddColumn<string>(
                name: "event_type",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.CreateIndex(
                name: "ix_price_rules_events_id_scope_priority",
                table: "price_rules",
                columns: new[] { "events_id", "scope", "priority" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_price_rules_Scope",
                table: "price_rules",
                sql: "(scope = 'Price' AND prices_id IS NOT NULL AND events_id IS NULL) OR (scope = 'Event' AND events_id IS NOT NULL AND prices_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_EventType",
                table: "events",
                sql: "event_type IN ('Open','Table','Both')");

            migrationBuilder.AddForeignKey(
                name: "fk_price_rules_events_events_id",
                table: "price_rules",
                column: "events_id",
                principalTable: "events",
                principalColumn: "events_id",
                onDelete: ReferentialAction.Cascade);

            // Backfill existing events from their layout_mode: Grid = table-based,
            // Open = open seating. New 'Open' column default only applies to fresh rows.
            migrationBuilder.Sql(
                "UPDATE events SET event_type = CASE WHEN layout_mode = 'Grid' THEN 'Table' ELSE 'Open' END");

            // Refresh SQL artifacts that now reference event_type / price_rules.scope:
            // resolve_price (event-wide rule fallback), event + ticket/table + price-rule
            // SPs (event_type gating, scoped rules), and vw_events (capacity by type).
            // CREATE OR REPLACE / DROP-CREATE make this idempotent.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_price_rules_events_events_id",
                table: "price_rules");

            migrationBuilder.DropIndex(
                name: "ix_price_rules_events_id_scope_priority",
                table: "price_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_price_rules_Scope",
                table: "price_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_EventType",
                table: "events");

            migrationBuilder.DropColumn(
                name: "events_id",
                table: "price_rules");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "price_rules");

            migrationBuilder.DropColumn(
                name: "event_type",
                table: "events");

            migrationBuilder.AlterColumn<Guid>(
                name: "prices_id",
                table: "price_rules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
