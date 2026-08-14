using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class FreeTicketsZeroFee : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("01_compute_fee.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
