using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class AddEventReminders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS event_reminders (
                    events_id uuid PRIMARY KEY REFERENCES events(events_id) ON DELETE CASCADE,
                    reminders_enabled boolean NOT NULL DEFAULT true,
                    reminder_7d_sent boolean NOT NULL DEFAULT false,
                    reminder_48h_sent boolean NOT NULL DEFAULT false,
                    last_manual_reminder_at timestamptz NULL,
                    manual_reminder_count integer NOT NULL DEFAULT 0,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NOT NULL DEFAULT now()
                );
            ");

            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_get_event_reminder_settings.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_set_event_reminder_settings.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_get_events_due_for_reminder.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_mark_event_reminder_sent.sql"));
            migrationBuilder.Sql(db.Migrations.MigrationSqlLoader.Load("sp_get_event_attendee_emails.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS event_reminders CASCADE;");
        }
    }
}
