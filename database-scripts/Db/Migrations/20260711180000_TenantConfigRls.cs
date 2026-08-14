using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class TenantConfigRls : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_tenant_identity;");
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_public_tenant_identity.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_get_public_tenant_branding.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("rls_tenants.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("rls_app_settings.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("rls_fee_formulas.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("rls_enum_definitions.sql"));
            migrationBuilder.Sql("GRANT EXECUTE ON FUNCTION sp_public_tenant_identity() TO ep_app;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
