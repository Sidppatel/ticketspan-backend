using System;
using db.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_lines",
                columns: table => new
                {
                    booking_lines_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bookings_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    event_ticket_types_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tables_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prices_id = table.Column<Guid>(type: "uuid", nullable: true),
                    seats = table.Column<int>(type: "integer", nullable: false),
                    subtotal_cents = table.Column<int>(type: "integer", nullable: false),
                    fee_cents = table.Column<int>(type: "integer", nullable: false),
                    total_cents = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_lines", x => x.booking_lines_id);
                    table.CheckConstraint("CK_booking_lines_FeeCents", "fee_cents >= 0");
                    table.CheckConstraint("CK_booking_lines_Kind", "kind IN ('Ticket','Table')");
                    table.CheckConstraint("CK_booking_lines_Ref", "(kind = 'Ticket' AND event_ticket_types_id IS NOT NULL AND tables_id IS NULL) OR (kind = 'Table' AND tables_id IS NOT NULL AND event_ticket_types_id IS NULL)");
                    table.CheckConstraint("CK_booking_lines_Seats", "seats > 0");
                    table.CheckConstraint("CK_booking_lines_SubtotalCents", "subtotal_cents >= 0");
                    table.CheckConstraint("CK_booking_lines_TotalFormula", "total_cents = subtotal_cents + fee_cents");
                    table.ForeignKey(
                        name: "fk_booking_lines_bookings_bookings_id",
                        column: x => x.bookings_id,
                        principalTable: "bookings",
                        principalColumn: "bookings_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_booking_lines_event_ticket_types_event_ticket_types_id",
                        column: x => x.event_ticket_types_id,
                        principalTable: "event_ticket_types",
                        principalColumn: "event_ticket_types_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_booking_lines_prices_prices_id",
                        column: x => x.prices_id,
                        principalTable: "prices",
                        principalColumn: "prices_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_booking_lines_tables_tables_id",
                        column: x => x.tables_id,
                        principalTable: "tables",
                        principalColumn: "tables_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_booking_lines_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_bookings_id",
                table: "booking_lines",
                column: "bookings_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_event_ticket_types_id",
                table: "booking_lines",
                column: "event_ticket_types_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_prices_id",
                table: "booking_lines",
                column: "prices_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_tables_id",
                table: "booking_lines",
                column: "tables_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_tenants_id",
                table: "booking_lines",
                column: "tenants_id");

            // Reinstall SQL artifacts now that booking_lines exists: the unified
            // seat-accounting helpers (05_seats_live), the multi-booking SP, the
            // confirm/remaining/reserve changes, and the booking_lines RLS policy.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_lines");
        }
    }
}
