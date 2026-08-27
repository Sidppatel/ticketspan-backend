using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class UniversalAttendeeAccounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- 1. Consolidate duplicate attendee accounts (role = 0) per email_hash across tenants
                DO $$
                BEGIN
                    CREATE TEMP TABLE IF NOT EXISTS tmp_user_merge AS
                    WITH ranked AS (
                        SELECT
                            users_id,
                            email_hash,
                            ROW_NUMBER() OVER (PARTITION BY email_hash ORDER BY created_at ASC) as rn,
                            FIRST_VALUE(users_id) OVER (PARTITION BY email_hash ORDER BY created_at ASC) as keeper_id
                        FROM users
                        WHERE role = 0
                    )
                    SELECT users_id AS old_id, keeper_id AS new_id
                    FROM ranked
                    WHERE rn > 1;

                    -- Re-point bookings to the primary attendee account
                    UPDATE bookings b
                    SET users_id = m.new_id
                    FROM tmp_user_merge m
                    WHERE b.users_id = m.old_id;

                    -- Re-point booking_lines to the primary attendee account
                    UPDATE booking_lines bl
                    SET guest_users_id = m.new_id
                    FROM tmp_user_merge m
                    WHERE bl.guest_users_id = m.old_id;

                    -- Re-point device sessions
                    DELETE FROM device_sessions ds
                    USING tmp_user_merge m
                    WHERE ds.users_id = m.old_id;

                    -- Clean up reset tokens for duplicate users
                    DELETE FROM password_reset_tokens prt
                    USING tmp_user_merge m
                    WHERE prt.users_id = m.old_id;

                    -- Re-point feedbacks
                    UPDATE feedbacks fb
                    SET users_id = m.new_id
                    FROM tmp_user_merge m
                    WHERE fb.users_id = m.old_id;

                    -- Delete merged duplicate attendee rows
                    DELETE FROM users u
                    USING tmp_user_merge m
                    WHERE u.users_id = m.old_id;

                    DROP TABLE IF EXISTS tmp_user_merge;
                END $$;

                -- 2. Drop existing DeveloperHasNoTenant constraint and obsolete indexes
                ALTER TABLE users DROP CONSTRAINT IF EXISTS ""CK_users_DeveloperHasNoTenant"";
                ALTER TABLE users DROP CONSTRAINT IF EXISTS ""CK_users_TenantScope"";
                DROP INDEX IF EXISTS ix_users_google_subject_tenants_id_role;

                -- 3. Set tenants_id = NULL for all attendee accounts
                UPDATE users SET tenants_id = NULL WHERE role = 0;

                -- 4. Add updated constraint: Developer (99) and Attendee (0) have NULL tenants_id; Organizer roles (1,2,3,4) require tenants_id
                ALTER TABLE users ADD CONSTRAINT ""CK_users_TenantScope""
                    CHECK ((role IN (0, 99) AND tenants_id IS NULL) OR (role NOT IN (0, 99) AND tenants_id IS NOT NULL));

                -- 5. Create unique indexes for global attendees
                CREATE UNIQUE INDEX IF NOT EXISTS uq_users_public_email_hash ON users (email_hash) WHERE (role = 0);
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_google_subject_attendee ON users (google_subject) WHERE (role = 0 AND google_subject IS NOT NULL);
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_google_subject_organizer ON users (google_subject, tenants_id, role) WHERE (role != 0 AND google_subject IS NOT NULL);
            ");

            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_signup_attendee.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_signin_user_google.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("rls_users.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ix_users_google_subject_organizer;
                DROP INDEX IF EXISTS ix_users_google_subject_attendee;
                DROP INDEX IF EXISTS uq_users_public_email_hash;
                ALTER TABLE users DROP CONSTRAINT IF EXISTS ""CK_users_TenantScope"";
                ALTER TABLE users ADD CONSTRAINT ""CK_users_DeveloperHasNoTenant"" CHECK ((role = 99) = (tenants_id IS NULL));
            ");
        }
    }
}
