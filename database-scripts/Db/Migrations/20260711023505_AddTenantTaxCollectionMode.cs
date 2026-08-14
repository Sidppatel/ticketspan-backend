using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class AddTenantTaxCollectionMode : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE tenants ADD COLUMN IF NOT EXISTS tax_collection_mode varchar(16) DEFAULT 'platform';");

            migrationBuilder.Sql("ALTER TABLE stripe_transactions ADD COLUMN IF NOT EXISTS payment_method_last4 text");

            migrationBuilder.Sql("ALTER TABLE stripe_transactions ADD COLUMN IF NOT EXISTS payment_method_type text");

            migrationBuilder.Sql("ALTER TABLE booking_taxes ADD COLUMN IF NOT EXISTS collected_by varchar(16) DEFAULT 'platform';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tenants_TaxCollectionMode",
                table: "tenants",
                sql: "tax_collection_mode IN ('platform','self')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_booking_taxes_CollectedBy",
                table: "booking_taxes",
                sql: "collected_by IN ('platform','self')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tenants_TaxCollectionMode",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_booking_taxes_CollectedBy",
                table: "booking_taxes");

            migrationBuilder.DropColumn(
                name: "tax_collection_mode",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "payment_method_last4",
                table: "stripe_transactions");

            migrationBuilder.DropColumn(
                name: "payment_method_type",
                table: "stripe_transactions");

            migrationBuilder.DropColumn(
                name: "collected_by",
                table: "booking_taxes");
        }
    }
}
