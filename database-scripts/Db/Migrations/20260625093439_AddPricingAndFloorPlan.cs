using System;
using Microsoft.EntityFrameworkCore.Migrations;
using db.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingAndFloorPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "default_fee_formulas_id",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "capacity_override",
                table: "tables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color_override",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shape_override",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "prices_id",
                table: "event_ticket_types",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "prices_id",
                table: "event_tables",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "floor_plan_templates",
                columns: table => new
                {
                    floor_plan_templates_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    grid_rows = table.Column<int>(type: "integer", nullable: false),
                    grid_cols = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floor_plan_templates", x => x.floor_plan_templates_id);
                    table.CheckConstraint("CK_floor_plan_templates_Grid", "(grid_rows IS NULL OR grid_rows > 0) AND (grid_cols IS NULL OR grid_cols > 0)");
                    table.ForeignKey(
                        name: "fk_floor_plan_templates_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "layout_objects",
                columns: table => new
                {
                    layout_objects_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    grid_row = table.Column<int>(type: "integer", nullable: false),
                    grid_col = table.Column<int>(type: "integer", nullable: false),
                    row_span = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    col_span = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_layout_objects", x => x.layout_objects_id);
                    table.CheckConstraint("CK_layout_objects_GridCol", "grid_col >= 0");
                    table.CheckConstraint("CK_layout_objects_GridRow", "grid_row >= 0");
                    table.CheckConstraint("CK_layout_objects_ObjectType", "object_type IN ('Entry','Exit','Stage')");
                    table.ForeignKey(
                        name: "fk_layout_objects_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_layout_objects_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prices",
                columns: table => new
                {
                    prices_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    pricing_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    base_price_cents = table.Column<int>(type: "integer", nullable: false),
                    per_attendee_cents = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_all_inclusive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    fee_formulas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_prices_id = table.Column<Guid>(type: "uuid", nullable: true),
                    max_quantity = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prices", x => x.prices_id);
                    table.CheckConstraint("CK_prices_BasePriceCents", "base_price_cents >= 0");
                    table.CheckConstraint("CK_prices_MaxQuantity", "max_quantity IS NULL OR max_quantity > 0");
                    table.CheckConstraint("CK_prices_PerAttendeeCents", "per_attendee_cents >= 0");
                    table.CheckConstraint("CK_prices_PricingType", "pricing_type IN ('TicketTier','Table','AddOn')");
                    table.ForeignKey(
                        name: "fk_prices_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_prices_fee_formulas_fee_formulas_id",
                        column: x => x.fee_formulas_id,
                        principalTable: "fee_formulas",
                        principalColumn: "fee_formulas_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_prices_prices_parent_prices_id",
                        column: x => x.parent_prices_id,
                        principalTable: "prices",
                        principalColumn: "prices_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prices_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "floor_plan_template_objects",
                columns: table => new
                {
                    floor_plan_template_objects_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor_plan_templates_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    grid_row = table.Column<int>(type: "integer", nullable: false),
                    grid_col = table.Column<int>(type: "integer", nullable: false),
                    row_span = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    col_span = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floor_plan_template_objects", x => x.floor_plan_template_objects_id);
                    table.CheckConstraint("CK_fpt_objects_ObjectType", "object_type IN ('Entry','Exit','Stage')");
                    table.ForeignKey(
                        name: "fk_floor_plan_template_objects_floor_plan_templates_floor_plan",
                        column: x => x.floor_plan_templates_id,
                        principalTable: "floor_plan_templates",
                        principalColumn: "floor_plan_templates_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_floor_plan_template_objects_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "floor_plan_template_tables",
                columns: table => new
                {
                    floor_plan_template_tables_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor_plan_templates_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    type_label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    grid_row = table.Column<int>(type: "integer", nullable: false),
                    grid_col = table.Column<int>(type: "integer", nullable: false),
                    row_span = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    col_span = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floor_plan_template_tables", x => x.floor_plan_template_tables_id);
                    table.CheckConstraint("CK_fpt_tables_Shape", "shape IN ('Round','Rectangle','Square','Cocktail')");
                    table.ForeignKey(
                        name: "fk_floor_plan_template_tables_floor_plan_templates_floor_plan_",
                        column: x => x.floor_plan_templates_id,
                        principalTable: "floor_plan_templates",
                        principalColumn: "floor_plan_templates_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_floor_plan_template_tables_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_rules",
                columns: table => new
                {
                    price_rules_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prices_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rule_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    active_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    active_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    min_remaining = table.Column<int>(type: "integer", nullable: true),
                    max_remaining = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_rules", x => x.price_rules_id);
                    table.CheckConstraint("CK_price_rules_PriceCents", "price_cents >= 0");
                    table.CheckConstraint("CK_price_rules_RuleType", "rule_type IN ('Presale','LastMinute','TimeWindow','Dynamic')");
                    table.CheckConstraint("CK_price_rules_Window", "active_from IS NULL OR active_until IS NULL OR active_until > active_from");
                    table.ForeignKey(
                        name: "fk_price_rules_prices_prices_id",
                        column: x => x.prices_id,
                        principalTable: "prices",
                        principalColumn: "prices_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_price_rules_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenants_default_fee_formulas_id",
                table: "tenants",
                column: "default_fee_formulas_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_ticket_types_prices_id",
                table: "event_ticket_types",
                column: "prices_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_tables_prices_id",
                table: "event_tables",
                column: "prices_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plan_template_objects_floor_plan_templates_id",
                table: "floor_plan_template_objects",
                column: "floor_plan_templates_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plan_template_objects_tenants_id",
                table: "floor_plan_template_objects",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plan_template_tables_floor_plan_templates_id",
                table: "floor_plan_template_tables",
                column: "floor_plan_templates_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plan_template_tables_tenants_id",
                table: "floor_plan_template_tables",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plan_templates_tenants_id",
                table: "floor_plan_templates",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_layout_objects_events_id",
                table: "layout_objects",
                column: "events_id");

            migrationBuilder.CreateIndex(
                name: "ix_layout_objects_tenants_id",
                table: "layout_objects",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_rules_prices_id_priority",
                table: "price_rules",
                columns: new[] { "prices_id", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_price_rules_tenants_id",
                table: "price_rules",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_prices_events_id",
                table: "prices",
                column: "events_id");

            migrationBuilder.CreateIndex(
                name: "ix_prices_fee_formulas_id",
                table: "prices",
                column: "fee_formulas_id");

            migrationBuilder.CreateIndex(
                name: "ix_prices_parent_prices_id",
                table: "prices",
                column: "parent_prices_id");

            migrationBuilder.CreateIndex(
                name: "ix_prices_tenants_id",
                table: "prices",
                column: "tenants_id");

            migrationBuilder.AddForeignKey(
                name: "fk_event_tables_prices_prices_id",
                table: "event_tables",
                column: "prices_id",
                principalTable: "prices",
                principalColumn: "prices_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_event_ticket_types_prices_prices_id",
                table: "event_ticket_types",
                column: "prices_id",
                principalTable: "prices",
                principalColumn: "prices_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tenants_fee_formulas_default_fee_formulas_id",
                table: "tenants",
                column: "default_fee_formulas_id",
                principalTable: "fee_formulas",
                principalColumn: "fee_formulas_id",
                onDelete: ReferentialAction.SetNull);

            // Re-install all SQL artifacts so the new/updated functions
            // (resolve_price), stored procedures (pricing + floor-plan), views and
            // RLS policies for the new tables are created. All are idempotent
            // (CREATE OR REPLACE / DROP POLICY IF EXISTS).
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_event_tables_prices_prices_id",
                table: "event_tables");

            migrationBuilder.DropForeignKey(
                name: "fk_event_ticket_types_prices_prices_id",
                table: "event_ticket_types");

            migrationBuilder.DropForeignKey(
                name: "fk_tenants_fee_formulas_default_fee_formulas_id",
                table: "tenants");

            migrationBuilder.DropTable(
                name: "floor_plan_template_objects");

            migrationBuilder.DropTable(
                name: "floor_plan_template_tables");

            migrationBuilder.DropTable(
                name: "layout_objects");

            migrationBuilder.DropTable(
                name: "price_rules");

            migrationBuilder.DropTable(
                name: "floor_plan_templates");

            migrationBuilder.DropTable(
                name: "prices");

            migrationBuilder.DropIndex(
                name: "ix_tenants_default_fee_formulas_id",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_event_ticket_types_prices_id",
                table: "event_ticket_types");

            migrationBuilder.DropIndex(
                name: "ix_event_tables_prices_id",
                table: "event_tables");

            migrationBuilder.DropColumn(
                name: "default_fee_formulas_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "capacity_override",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "color_override",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "shape_override",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "prices_id",
                table: "event_ticket_types");

            migrationBuilder.DropColumn(
                name: "prices_id",
                table: "event_tables");
        }
    }
}
