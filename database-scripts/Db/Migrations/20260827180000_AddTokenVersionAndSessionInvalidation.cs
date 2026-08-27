using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class AddTokenVersionAndSessionInvalidation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public' 
                          AND table_name = 'users' 
                          AND column_name = 'token_version'
                    ) THEN
                        ALTER TABLE users ADD COLUMN token_version integer DEFAULT 1 NOT NULL;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0150_v_signin_public.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0151_v_signin_admin.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0152_v_signin_staff.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0153_v_signin_developer.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_set_user_password.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
