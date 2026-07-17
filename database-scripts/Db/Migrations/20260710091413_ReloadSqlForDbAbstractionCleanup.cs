using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class ReloadSqlForDbAbstractionCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE stripe_transactions ADD COLUMN IF NOT EXISTS payment_method_type text;");
            migrationBuilder.Sql("ALTER TABLE stripe_transactions ADD COLUMN IF NOT EXISTS payment_method_last4 text;");
            migrationBuilder.Sql("ALTER TABLE stripe_transactions ADD COLUMN IF NOT EXISTS payment_method_brand text;");

            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.security");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
