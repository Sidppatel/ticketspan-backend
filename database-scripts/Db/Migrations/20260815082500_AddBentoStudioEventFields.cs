using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    public partial class AddBentoStudioEventFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE events ADD COLUMN IF NOT EXISTS short_description TEXT;
                ALTER TABLE events ADD COLUMN IF NOT EXISTS story_description TEXT;
                ALTER TABLE events ADD COLUMN IF NOT EXISTS hero_backdrop_image_id UUID REFERENCES images(images_id);
                ALTER TABLE events ADD COLUMN IF NOT EXISTS poster_image_id UUID REFERENCES images(images_id);
                ALTER TABLE events ADD COLUMN IF NOT EXISTS is_verified_organizer BOOLEAN DEFAULT TRUE;
                ALTER TABLE events ADD COLUMN IF NOT EXISTS urgency_badge_text VARCHAR(100);

                DROP FUNCTION IF EXISTS sp_create_event CASCADE;
                DROP FUNCTION IF EXISTS sp_update_event CASCADE;
                DROP FUNCTION IF EXISTS sp_list_event_images(uuid);
                DROP FUNCTION IF EXISTS sp_list_event_images(uuid, text);
            ");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            db.Migrations.MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored-procedures");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
