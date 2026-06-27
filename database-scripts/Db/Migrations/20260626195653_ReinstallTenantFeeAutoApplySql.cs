using db.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class ReinstallTenantFeeAutoApplySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL-only refresh: the sellable create/update SPs now resolve the cached
            // platform_fee_cents through app.resolve_fee_formula, so a new ticket
            // tier / table with no explicit override auto-inherits the tenant's
            // default fee (previously cached 0). CREATE OR REPLACE = idempotent.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
