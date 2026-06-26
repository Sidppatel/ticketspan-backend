using db.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class ReinstallPricingWiringSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pure SQL-artifact refresh (no schema change): wires every sellable to
            // a real Pricing Module price and feeds live inventory into rule
            // selection. Adds app.create_price + app.remaining_for_price, makes
            // sp_create_event_table / sp_create_event_ticket_type link a price, and
            // threads app.remaining_for_price through sp_create_purchase /
            // sp_calculate_price. CREATE OR REPLACE makes this idempotent.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
