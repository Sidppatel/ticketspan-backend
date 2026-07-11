using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP VIEW IF EXISTS vw_bookings_by_status CASCADE;
                DROP VIEW IF EXISTS vw_device_sessions CASCADE;
                DROP VIEW IF EXISTS vw_event_facets CASCADE;
                DROP VIEW IF EXISTS vw_event_summary CASCADE;
                DROP VIEW IF EXISTS vw_event_table_stats CASCADE;
                DROP VIEW IF EXISTS vw_event_tables_summary CASCADE;
                DROP VIEW IF EXISTS vw_events_by_category CASCADE;
                DROP VIEW IF EXISTS vw_platform_images CASCADE;
                DROP VIEW IF EXISTS vw_site_visits CASCADE;
                DROP VIEW IF EXISTS vw_tables CASCADE;
                DROP VIEW IF EXISTS vw_top_events_revenue CASCADE;
                DROP VIEW IF EXISTS vw_user_events CASCADE;
                DROP VIEW IF EXISTS vw_users CASCADE;
                DROP FUNCTION IF EXISTS sp_create_table(uuid,uuid,text,numeric,numeric,integer,numeric,numeric) CASCADE;
                DROP FUNCTION IF EXISTS sp_count_admin_logs(text,text,timestamp with time zone,timestamp with time zone) CASCADE;
                DROP FUNCTION IF EXISTS sp_list_tenants(text,boolean,integer,integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_publish_scheduled_events() CASCADE;
                DROP FUNCTION IF EXISTS sp_update_user(uuid,text,text,text,smallint,boolean,uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_booking_info_for_event(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_cleanup_old_logs(integer,integer,integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_update_table(uuid,text,uuid,numeric,numeric,boolean,integer,numeric,numeric) CASCADE;
                DROP FUNCTION IF EXISTS sp_add_venue_image(uuid,text,text,integer,integer,integer,uuid,text,text,text,text,text) CASCADE;
                DROP FUNCTION IF EXISTS sp_relink_orphan_ticket_types(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_clear_tenant_stripe_account(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_upsert_setting(text,text,text) CASCADE;
                DROP FUNCTION IF EXISTS sp_create_stripe_transaction(uuid,text,integer,integer,text,text) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_tenant_by_user(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_update_user_password(uuid,text) CASCADE;
                DROP FUNCTION IF EXISTS sp_user_event_exists(uuid,uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_count_performers(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_upsert_user(uuid,text,text,text,text,smallint) CASCADE;
                DROP FUNCTION IF EXISTS sp_user_exists_by_email(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_event_stats(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_add_platform_image(text,text,integer,integer,integer,uuid,text,text,text,text,text,text) CASCADE;
                DROP FUNCTION IF EXISTS sp_consume_user_email_verification_token(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_delete_image(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_cleanup_expired_locks() CASCADE;
                DROP FUNCTION IF EXISTS sp_event_table_has_locked_tables(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_invitation_by_token_hash(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_locked_table_ids(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_primary_image_key(text,uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_delete_table(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_event_by_id_for_layout(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_event_last_checkin(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_list_event_tables_for_event(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_stripe_transaction_by_intent(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_list_users() CASCADE;
                DROP FUNCTION IF EXISTS sp_get_table_by_id(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_reset_user_lockout(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_list_existing_event_table_template_ids(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_mark_table_booked(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_create_user_email_verification_token(uuid,text,timestamp with time zone,text) CASCADE;
                DROP FUNCTION IF EXISTS sp_list_tables_for_event(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_pending_invitation_by_email(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_create_device_session(uuid,text,text,text,text,timestamp with time zone) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_admin_logs(text,text,timestamp with time zone,timestamp with time zone,integer,integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_delete_user(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_set_platform_primary_image(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_event_table_by_id(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_increment_user_failed_login(uuid,integer,integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_event_has_active_bookings(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_next_event_dashboard(timestamp with time zone) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_email_logs(text,integer,integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_table_template_by_id(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_cleanup_expired_sessions() CASCADE;
                DROP FUNCTION IF EXISTS sp_list_platform_images(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_user_by_email(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_list_active_table_templates() CASCADE;
                DROP FUNCTION IF EXISTS sp_update_event_table(uuid,text,integer,text,text,integer,boolean,uuid,numeric,numeric) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_system_logs(timestamp with time zone,text,text,integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_monthly_report_summary(integer,integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_create_email_log(text,text,text,text) CASCADE;
                DROP FUNCTION IF EXISTS sp_set_stripe_tax_transaction_id(text,text) CASCADE;
                DROP FUNCTION IF EXISTS sp_claim_ticket(text,uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_remove_platform_image(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_count_developer_logs(text,text,timestamp with time zone,timestamp with time zone) CASCADE;
                DROP FUNCTION IF EXISTS fn_audit_trigger() CASCADE;
                DROP FUNCTION IF EXISTS sp_event_table_has_active_bookings(uuid,uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_search_performers(text,integer,integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_list_active_table_templates_by_ids(uuid[]) CASCADE;
                DROP FUNCTION IF EXISTS sp_revoke_all_user_sessions(uuid,text) CASCADE;
                DROP FUNCTION IF EXISTS sp_reorder_platform_images(uuid[]) CASCADE;
                DROP FUNCTION IF EXISTS sp_clear_user_image(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_developer_logs(text,text,timestamp with time zone,timestamp with time zone,integer,integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_update_session_activity(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_add_event_image(uuid,text,text,integer,integer,integer,uuid,text,text,text,text,text) CASCADE;
                DROP FUNCTION IF EXISTS sp_count_email_logs(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_event_recent_bookings(uuid,integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_user_by_id(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_invalidate_password_reset_token(text) CASCADE;
                DROP FUNCTION IF EXISTS sp_invite_ticket(uuid,text,text,timestamp with time zone) CASCADE;
                DROP FUNCTION IF EXISTS sp_set_user_active(uuid,boolean) CASCADE;
                """);

            migrationBuilder.DropTable(
                name: "platform_images");

            migrationBuilder.DropTable(
                name: "user_email_verification_tokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_images",
                columns: table => new
                {
                    platform_images_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    images_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_images", x => x.platform_images_id);
                    table.ForeignKey(
                        name: "fk_platform_images_images_images_id",
                        column: x => x.images_id,
                        principalTable: "images",
                        principalColumn: "images_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_email_verification_tokens",
                columns: table => new
                {
                    user_email_verification_tokens_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    users_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_email_verification_tokens", x => x.user_email_verification_tokens_id);
                    table.ForeignKey(
                        name: "fk_user_email_verification_tokens_users_users_id",
                        column: x => x.users_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_images_images_id",
                table: "platform_images",
                column: "images_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_platform_images_sort_order",
                table: "platform_images",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "ix_platform_images_tag",
                table: "platform_images",
                column: "tag");

            migrationBuilder.CreateIndex(
                name: "ix_user_email_verification_tokens_expires_at",
                table: "user_email_verification_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_user_email_verification_tokens_token_hash",
                table: "user_email_verification_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_email_verification_tokens_users_id",
                table: "user_email_verification_tokens",
                column: "users_id");
        }
    }
}
