using db.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class InstallScheduleItemsSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.security");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
