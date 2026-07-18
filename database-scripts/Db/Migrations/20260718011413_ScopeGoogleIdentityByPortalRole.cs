using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class ScopeGoogleIdentityByPortalRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_google_subject",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_users_google_subject_tenants_id_role",
                table: "users",
                columns: new[] { "google_subject", "tenants_id", "role" },
                unique: true,
                filter: "google_subject IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_google_subject_tenants_id_role",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_users_google_subject",
                table: "users",
                column: "google_subject",
                unique: true,
                filter: "google_subject IS NOT NULL");
        }
    }
}
