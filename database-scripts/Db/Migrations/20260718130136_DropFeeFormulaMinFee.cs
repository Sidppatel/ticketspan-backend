using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class DropFeeFormulaMinFee : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("01_compute_fee.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("03_tier_pricing.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_fee_formulas.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_billing.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_set_tenant_tier.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0106_v_developer_billing.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0145_v_fee_formulas.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0200_fix_security_invoker.sql"));

            migrationBuilder.DropColumn(
                name: "min_fee_cents",
                table: "fee_formulas");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "min_fee_cents",
                table: "fee_formulas",
                type: "integer",
                nullable: true);
        }
    }
}
