using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class AddTokenVersionToVwUserProfile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0124_v_user_profile.sql"));
            migrationBuilder.Sql("ALTER VIEW vw_user_profile SET (security_invoker = true);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
