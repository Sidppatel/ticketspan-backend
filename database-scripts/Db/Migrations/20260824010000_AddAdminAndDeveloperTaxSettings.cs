using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class AddAdminAndDeveloperTaxSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE tenants ADD COLUMN IF NOT EXISTS charge_tax_by_default boolean DEFAULT TRUE;
            ");

            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0135_v_tenant_reporting_access.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0131_02_v_events_with_sponsors.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_create_event.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_update_event.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_set_tenant_tax_default.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_set_event_tax_exempt.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS sp_set_event_tax_exempt(uuid, bool);
                DROP FUNCTION IF EXISTS sp_set_tenant_tax_default(uuid, bool);
                ALTER TABLE tenants DROP COLUMN IF EXISTS charge_tax_by_default;
            ");
        }
    }
}
