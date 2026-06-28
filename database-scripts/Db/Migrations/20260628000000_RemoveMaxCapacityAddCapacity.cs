using db.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMaxCapacityAddCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the event-dependent views before dropping events.max_capacity
            // (Postgres blocks dropping a column a view binds to). The SQL reinstall
            // at the end recreates the full views folder against the new schema.
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_events CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_event_summary CASCADE;");

            migrationBuilder.DropCheckConstraint(name: "CK_events_MaxCapacity", table: "events");
            migrationBuilder.DropColumn(name: "max_capacity", table: "events");

            // Per-ticket-type capacity feeds the calculated event capacity (view).
            migrationBuilder.AddColumn<int>(name: "capacity", table: "event_ticket_types",
                type: "integer", nullable: true);
            migrationBuilder.AddCheckConstraint(name: "CK_event_ticket_types_Capacity",
                table: "event_ticket_types", sql: "capacity IS NULL OR capacity > 0");

            // Seat/people cap a price rule's discount applies to.
            migrationBuilder.AddColumn<int>(name: "capacity", table: "price_rules",
                type: "integer", nullable: true);
            migrationBuilder.AddCheckConstraint(name: "CK_price_rules_Capacity",
                table: "price_rules", sql: "capacity IS NULL OR capacity > 0");

            // Reinstall SQL artifacts: views now compute total_capacity purely from
            // ticket-type / table capacity; procs drop their pre-capacity signatures
            // and recreate with the new params.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema-only reversal. SQL procs/views are NOT reverted (the .sql files in
            // the assembly are the capacity-era versions); re-run a prior migration's
            // artifacts manually if a full rollback is ever needed.
            migrationBuilder.DropCheckConstraint(name: "CK_price_rules_Capacity", table: "price_rules");
            migrationBuilder.DropColumn(name: "capacity", table: "price_rules");

            migrationBuilder.DropCheckConstraint(name: "CK_event_ticket_types_Capacity", table: "event_ticket_types");
            migrationBuilder.DropColumn(name: "capacity", table: "event_ticket_types");

            migrationBuilder.AddColumn<int>(name: "max_capacity", table: "events",
                type: "integer", nullable: true);
            migrationBuilder.AddCheckConstraint(name: "CK_events_MaxCapacity",
                table: "events", sql: "max_capacity IS NULL OR max_capacity > 0");
        }
    }
}
