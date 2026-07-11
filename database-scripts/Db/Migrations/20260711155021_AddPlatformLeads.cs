using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_leads",
                columns: table => new
                {
                    platform_leads_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    website = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_leads", x => x.platform_leads_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_leads_created_at",
                table: "platform_leads",
                column: "created_at");

            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_create_platform_lead.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0148_v_platform_leads.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("rls_platform_leads.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_leads");
        }
    }
}
