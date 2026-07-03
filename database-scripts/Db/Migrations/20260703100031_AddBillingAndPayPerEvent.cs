using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingAndPayPerEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "fee_formulas_id",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fee_override_expires_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "billing_charges",
                columns: table => new
                {
                    billing_charges_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    events_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_cents = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    stripe_payment_intent_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_billing_charges", x => x.billing_charges_id);
                    table.CheckConstraint("CK_billing_charges_Kind", "kind IN ('subscription','proration','pay_per_event','addon','setup_fee','refund')");
                    table.ForeignKey(
                        name: "fk_billing_charges_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_upgrades",
                columns: table => new
                {
                    event_upgrades_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    sms_credits = table.Column<int>(type: "integer", nullable: false),
                    custom_domain_limit = table.Column<int>(type: "integer", nullable: false),
                    canceled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refunded_cents = table.Column<int>(type: "integer", nullable: false),
                    stripe_payment_intent_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_upgrades", x => x.event_upgrades_id);
                    table.CheckConstraint("CK_event_upgrades_Status", "status IN ('active','canceled','refunded')");
                    table.CheckConstraint("CK_event_upgrades_Tier", "tier IN ('starter_event','pro_event','business_event','enterprise_event')");
                    table.ForeignKey(
                        name: "fk_event_upgrades_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_upgrades_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_addons",
                columns: table => new
                {
                    tenant_addons_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    billing_period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "monthly"),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    setup_fee_cents = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usage_count = table.Column<int>(type: "integer", nullable: false),
                    canceled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_addons", x => x.tenant_addons_id);
                    table.CheckConstraint("CK_tenant_addons_BillingPeriod", "billing_period IN ('monthly','annual')");
                    table.CheckConstraint("CK_tenant_addons_Quantity", "quantity > 0");
                    table.CheckConstraint("CK_tenant_addons_Status", "status IN ('active','canceled')");
                    table.CheckConstraint("CK_tenant_addons_Type", "type IN ('custom_domain','advanced_analytics','sms','extra_manager')");
                    table.ForeignKey(
                        name: "fk_tenant_addons_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscriptions",
                columns: table => new
                {
                    tenant_subscriptions_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    monthly_price_cents = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false),
                    pending_tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    trial_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    trial_reminder_day_sent = table.Column<int>(type: "integer", nullable: false),
                    canceled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_payment_count = table.Column<int>(type: "integer", nullable: false),
                    stripe_subscription_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_subscriptions", x => x.tenant_subscriptions_id);
                    table.CheckConstraint("CK_tenant_subscriptions_Status", "status IN ('trial','active','past_due','canceled','expired')");
                    table.CheckConstraint("CK_tenant_subscriptions_Tier", "tier IN ('starter','professional','business','enterprise')");
                    table.ForeignKey(
                        name: "fk_tenant_subscriptions_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_events_fee_formulas_id",
                table: "events",
                column: "fee_formulas_id");

            migrationBuilder.CreateIndex(
                name: "ix_billing_charges_created_at",
                table: "billing_charges",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_billing_charges_tenants_id",
                table: "billing_charges",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_upgrades_live",
                table: "event_upgrades",
                column: "events_id",
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "ix_event_upgrades_tenants_id",
                table: "event_upgrades",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_addons_tenants_id",
                table: "tenant_addons",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_subscriptions_live",
                table: "tenant_subscriptions",
                column: "tenants_id",
                unique: true,
                filter: "status IN ('trial','active','past_due')");

            migrationBuilder.AddForeignKey(
                name: "fk_events_fee_formulas_fee_formulas_id",
                table: "events",
                column: "fee_formulas_id",
                principalTable: "fee_formulas",
                principalColumn: "fee_formulas_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_fee_formulas_fee_formulas_id",
                table: "events");

            migrationBuilder.DropTable(
                name: "billing_charges");

            migrationBuilder.DropTable(
                name: "event_upgrades");

            migrationBuilder.DropTable(
                name: "tenant_addons");

            migrationBuilder.DropTable(
                name: "tenant_subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_events_fee_formulas_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "fee_formulas_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "fee_override_expires_at",
                table: "events");
        }
    }
}
