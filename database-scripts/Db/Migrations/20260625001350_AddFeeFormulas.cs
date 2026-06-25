using System;
using Microsoft.EntityFrameworkCore.Migrations;
using db.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeFormulas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "fee_formulas_id",
                table: "event_ticket_types",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fee_formulas_id",
                table: "event_tables",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fee_formulas",
                columns: table => new
                {
                    fee_formulas_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    percent_bps = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    flat_cents = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    min_fee_cents = table.Column<int>(type: "integer", nullable: true),
                    max_fee_cents = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fee_formulas", x => x.fee_formulas_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_ticket_types_fee_formulas_id",
                table: "event_ticket_types",
                column: "fee_formulas_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_tables_fee_formulas_id",
                table: "event_tables",
                column: "fee_formulas_id");

            migrationBuilder.AddForeignKey(
                name: "fk_event_tables_fee_formulas_fee_formulas_id",
                table: "event_tables",
                column: "fee_formulas_id",
                principalTable: "fee_formulas",
                principalColumn: "fee_formulas_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_event_ticket_types_fee_formulas_fee_formulas_id",
                table: "event_ticket_types",
                column: "fee_formulas_id",
                principalTable: "fee_formulas",
                principalColumn: "fee_formulas_id",
                onDelete: ReferentialAction.SetNull);

            // RLS: any authenticated session may read fee formulas (tenants pick
            // one, public checkout displays the split); only developers write.
            // Inlined here rather than as a policies/ file because
            // InstallSqlArtifacts runs before this table exists.
            migrationBuilder.Sql(@"
ALTER TABLE fee_formulas ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS p_fee_formulas ON fee_formulas;
CREATE POLICY p_fee_formulas ON fee_formulas
    USING (true)
    WITH CHECK (app.is_developer());");

            // Reinstall functions (adds app.compute_fee) and stored procedures
            // (fee-formula CRUD + the now server-authoritative booking SPs). Both
            // are CREATE OR REPLACE / idempotent and now that fee_formulas + the
            // fee_formulas_id columns exist they install cleanly.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_event_tables_fee_formulas_fee_formulas_id",
                table: "event_tables");

            migrationBuilder.DropForeignKey(
                name: "fk_event_ticket_types_fee_formulas_fee_formulas_id",
                table: "event_ticket_types");

            migrationBuilder.DropTable(
                name: "fee_formulas");

            migrationBuilder.DropIndex(
                name: "ix_event_ticket_types_fee_formulas_id",
                table: "event_ticket_types");

            migrationBuilder.DropIndex(
                name: "ix_event_tables_fee_formulas_id",
                table: "event_tables");

            migrationBuilder.DropColumn(
                name: "fee_formulas_id",
                table: "event_ticket_types");

            migrationBuilder.DropColumn(
                name: "fee_formulas_id",
                table: "event_tables");
        }
    }
}
