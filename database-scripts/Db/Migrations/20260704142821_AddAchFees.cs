using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddAchFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ach_enabled",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ach_fee_formulas_id",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ach_enabled",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_ach_fee_formulas_id",
                table: "tenants",
                column: "ach_fee_formulas_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tenants_fee_formulas_ach_fee_formulas_id",
                table: "tenants",
                column: "ach_fee_formulas_id",
                principalTable: "fee_formulas",
                principalColumn: "fee_formulas_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tenants_fee_formulas_ach_fee_formulas_id",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_ach_fee_formulas_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ach_enabled",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ach_fee_formulas_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ach_enabled",
                table: "events");
        }
    }
}
