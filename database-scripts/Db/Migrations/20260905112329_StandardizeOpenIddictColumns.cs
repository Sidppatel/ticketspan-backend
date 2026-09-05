using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeOpenIddictColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OpenIddictTokens_id",
                table: "OpenIddictTokens",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "OpenIddictScopes_id",
                table: "OpenIddictScopes",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "OpenIddictAuthorizations_id",
                table: "OpenIddictAuthorizations",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "OpenIddictApplications_id",
                table: "OpenIddictApplications",
                newName: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "id",
                table: "OpenIddictTokens",
                newName: "OpenIddictTokens_id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "OpenIddictScopes",
                newName: "OpenIddictScopes_id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "OpenIddictAuthorizations",
                newName: "OpenIddictAuthorizations_id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "OpenIddictApplications",
                newName: "OpenIddictApplications_id");
        }
    }
}
