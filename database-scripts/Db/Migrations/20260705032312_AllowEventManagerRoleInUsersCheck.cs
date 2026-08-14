using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class AllowEventManagerRoleInUsersCheck : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_users_Role",
                table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_users_Role",
                table: "users",
                sql: "role IN (0, 1, 2, 3, 4, 99)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_users_Role",
                table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_users_Role",
                table: "users",
                sql: "role IN (0, 1, 2, 3, 99)");
        }
    }
}
