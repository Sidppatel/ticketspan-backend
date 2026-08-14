using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class FixSecurityInvoker : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0200_fix_security_invoker.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
