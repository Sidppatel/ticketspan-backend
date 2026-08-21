using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class ConfigurableEventReminders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE event_reminders ADD COLUMN IF NOT EXISTS reminder_1_hours integer NULL;
                ALTER TABLE event_reminders ADD COLUMN IF NOT EXISTS reminder_2_hours integer NULL;

                INSERT INTO app_settings (app_settings_id, key, value, description, created_at, updated_at)
                VALUES (gen_random_uuid(), 'event_reminder_1_hours', '168', 'First automated event reminder lead time in hours (default: 168 = 7 days)', now(), now())
                ON CONFLICT (key) DO NOTHING;

                INSERT INTO app_settings (app_settings_id, key, value, description, created_at, updated_at)
                VALUES (gen_random_uuid(), 'event_reminder_2_hours', '48', 'Second automated event reminder lead time in hours (default: 48 = 48 hours)', now(), now())
                ON CONFLICT (key) DO NOTHING;

                DROP FUNCTION IF EXISTS sp_get_event_reminder_settings(uuid);
                DROP FUNCTION IF EXISTS sp_get_events_due_for_reminder();
            ");

            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_get_event_reminder_settings.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_set_event_reminder_settings.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_get_events_due_for_reminder.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_mark_event_reminder_sent.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_get_event_attendee_emails.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE event_reminders DROP COLUMN IF EXISTS reminder_1_hours;
                ALTER TABLE event_reminders DROP COLUMN IF EXISTS reminder_2_hours;
            ");
        }
    }
}
