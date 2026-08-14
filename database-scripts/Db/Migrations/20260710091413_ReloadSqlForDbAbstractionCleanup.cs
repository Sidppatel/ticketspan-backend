using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class ReloadSqlForDbAbstractionCleanup : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE stripe_transactions ADD COLUMN IF NOT EXISTS payment_method_type text;");
            migrationBuilder.Sql("ALTER TABLE stripe_transactions ADD COLUMN IF NOT EXISTS payment_method_last4 text;");
            migrationBuilder.Sql("ALTER TABLE stripe_transactions ADD COLUMN IF NOT EXISTS payment_method_brand text;");

            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_calculate_price.sql"));
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.security");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
