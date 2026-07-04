using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogEventsId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "events_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenants_id_events_id_created_at",
                table: "audit_logs",
                columns: new[] { "tenants_id", "events_id", "created_at" });

            migrationBuilder.Sql(@"
                UPDATE audit_logs a SET events_id = p.events_id
                FROM prices p
                WHERE a.subject_type = 'prices' AND a.subject_id = p.prices_id AND a.events_id IS NULL;");
            migrationBuilder.Sql(@"
                UPDATE audit_logs a SET events_id = pr.events_id
                FROM price_rules pr
                WHERE a.subject_type = 'price_rules' AND a.subject_id = pr.price_rules_id AND a.events_id IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_logs_tenants_id_events_id_created_at",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "events_id",
                table: "audit_logs");
        }
    }
}
