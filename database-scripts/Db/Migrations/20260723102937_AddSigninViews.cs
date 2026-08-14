using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class AddSigninViews : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0150_v_signin_public.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0151_v_signin_admin.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0152_v_signin_staff.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0153_v_signin_developer.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_signin_public;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_signin_admin;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_signin_staff;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_signin_developer;");
        }
    }
}
