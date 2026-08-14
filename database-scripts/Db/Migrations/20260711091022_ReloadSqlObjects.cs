using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class ReloadSqlObjects : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                BEGIN
                    FOR r IN (SELECT viewname FROM pg_views WHERE schemaname = 'public' AND viewname LIKE 'vw_%') LOOP
                        EXECUTE 'DROP VIEW IF EXISTS ' || quote_ident(r.viewname) || ' CASCADE';
                    END LOOP;
                END $$;
            ");

            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.functions");
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_calculate_price.sql"));
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.policies");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.security");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
