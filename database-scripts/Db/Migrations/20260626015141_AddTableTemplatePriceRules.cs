using System;
using db.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddTableTemplatePriceRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "table_template_price_rules",
                columns: table => new
                {
                    table_template_price_rules_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_templates_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_table_template_price_rules", x => x.table_template_price_rules_id);
                    table.CheckConstraint("CK_table_template_price_rules_PriceCents", "price_cents >= 0");
                    table.CheckConstraint("CK_table_template_price_rules_RuleType", "rule_type IN ('Presale','LastMinute','TimeWindow','Dynamic')");
                    table.CheckConstraint("CK_table_template_price_rules_Window", "active_from IS NULL OR active_until IS NULL OR active_until > active_from");
                    table.ForeignKey(
                        name: "fk_table_template_price_rules_table_templates_table_templates_",
                        column: x => x.table_templates_id,
                        principalTable: "table_templates",
                        principalColumn: "table_templates_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_table_template_price_rules_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_table_template_price_rules_table_templates_id_priority",
                table: "table_template_price_rules",
                columns: new[] { "table_templates_id", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_table_template_price_rules_tenants_id",
                table: "table_template_price_rules",
                column: "tenants_id");

            // Re-install SQL artifacts so the catalog rule SPs (create/list/delete),
            // the snapshot logic in sp_create_event_table and the new RLS policy land.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "table_template_price_rules");
        }
    }
}
