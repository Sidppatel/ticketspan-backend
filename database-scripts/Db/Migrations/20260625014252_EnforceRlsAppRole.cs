using Microsoft.EntityFrameworkCore.Migrations;
using db.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class EnforceRlsAppRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-run security functions so app.is_developer() is SECURITY DEFINER
            // (prevents infinite RLS recursion on the users policy).
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            // Re-run stored procedures so pre-auth token SPs (password reset, magic
            // link, set password) are SECURITY DEFINER — under ep_app they run with
            // no session context and would otherwise be blocked by RLS.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            // Create least-privilege ep_app role, grant DML, FORCE row level
            // security, and flip views to security_invoker so RLS is enforced.
            // Must run after tables/views/policies exist (it does — later migration).
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.security");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
