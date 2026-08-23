using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class AddUserProfileBioPronounsBilling : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE users ADD COLUMN IF NOT EXISTS bio text;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS pronouns varchar(64);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS preferences_json jsonb;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS billing_addresses_id uuid REFERENCES addresses(addresses_id) ON DELETE SET NULL;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS stripe_customer_id varchar(255);
                CREATE INDEX IF NOT EXISTS ix_users_billing_addresses_id ON users (billing_addresses_id);
            ");

            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("0124_v_user_profile.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_update_user_profile.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_get_or_set_user_stripe_customer.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ix_users_billing_addresses_id;
                ALTER TABLE users DROP COLUMN IF EXISTS stripe_customer_id;
                ALTER TABLE users DROP COLUMN IF EXISTS billing_addresses_id;
                ALTER TABLE users DROP COLUMN IF EXISTS preferences_json;
                ALTER TABLE users DROP COLUMN IF EXISTS pronouns;
                ALTER TABLE users DROP COLUMN IF EXISTS bio;
            ");
        }
    }
}
