using System;
using Microsoft.EntityFrameworkCore.Migrations;
using db.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "tax_exempt",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate_override",
                table: "events",
                type: "numeric(6,5)",
                precision: 6,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "tax_calculated_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tax_cents",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tax_city",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_county",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate",
                table: "bookings",
                type: "numeric(6,5)",
                precision: 6,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_state",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_taxes",
                columns: table => new
                {
                    booking_taxes_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bookings_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zip_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    county = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    combined_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    state_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    county_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    city_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    local_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    taxable_amount_cents = table.Column<int>(type: "integer", nullable: false),
                    tax_amount_cents = table.Column<int>(type: "integer", nullable: false),
                    api_response_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    calculated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_taxes", x => x.booking_taxes_id);
                    table.CheckConstraint("CK_booking_taxes_TaxableAmountCents", "taxable_amount_cents >= 0");
                    table.CheckConstraint("CK_booking_taxes_TaxAmountCents", "tax_amount_cents >= 0");
                    table.ForeignKey(
                        name: "fk_booking_taxes_bookings_bookings_id",
                        column: x => x.bookings_id,
                        principalTable: "bookings",
                        principalColumn: "bookings_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_booking_taxes_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_rate_cache",
                columns: table => new
                {
                    zip_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    county = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    state_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    county_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    city_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    local_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    combined_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    api_response_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tax_rate_cache", x => x.zip_code);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_taxes_bookings_id",
                table: "booking_taxes",
                column: "bookings_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_booking_taxes_state",
                table: "booking_taxes",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "ix_booking_taxes_tenants_id",
                table: "booking_taxes",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_rate_cache_state",
                table: "tax_rate_cache",
                column: "state");

            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.security");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_taxes");

            migrationBuilder.DropTable(
                name: "tax_rate_cache");

            migrationBuilder.DropColumn(
                name: "tax_exempt",
                table: "events");

            migrationBuilder.DropColumn(
                name: "tax_rate_override",
                table: "events");

            migrationBuilder.DropColumn(
                name: "tax_calculated_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "tax_cents",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "tax_city",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "tax_county",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "tax_rate",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "tax_state",
                table: "bookings");
        }
    }
}
