using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Db.Migrations
{
    
    public partial class InitialCreate : Migration
    {
        
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.CreateTable(
                name: "addresses",
                columns: table => new
                {
                    addresses_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    line1 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    line2 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    zip_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_addresses", x => x.addresses_id);
                });

            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    app_settings_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_settings", x => x.app_settings_id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    audit_logs_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    actor_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.audit_logs_id);
                    table.CheckConstraint("CK_audit_logs_ActorType", "actor_type IN ('User','Admin','Developer','System')");
                });

            migrationBuilder.CreateTable(
                name: "email_logs",
                columns: table => new
                {
                    email_logs_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    recipient = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    subject = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_logs", x => x.email_logs_id);
                });

            migrationBuilder.CreateTable(
                name: "enum_definitions",
                columns: table => new
                {
                    enum_definitions_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    enum_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    enum_value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    int_value = table.Column<int>(type: "integer", nullable: false),
                    used_in = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enum_definitions", x => x.enum_definitions_id);
                });

            migrationBuilder.CreateTable(
                name: "fee_formulas",
                columns: table => new
                {
                    fee_formulas_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    percent_bps = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    flat_cents = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    min_fee_cents = table.Column<int>(type: "integer", nullable: true),
                    max_fee_cents = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fee_formulas", x => x.fee_formulas_id);
                });

            migrationBuilder.CreateTable(
                name: "magic_link_tokens",
                columns: table => new
                {
                    magic_link_tokens_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_magic_link_tokens", x => x.magic_link_tokens_id);
                    table.CheckConstraint("CK_magic_link_tokens_Usage", "(is_used = false AND used_at IS NULL) OR (is_used = true AND used_at IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "US"),
                    stripe_connected_account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    stripe_charges_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    stripe_payouts_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    stripe_details_submitted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    stripe_onboarded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stripe_requirements_due = table.Column<string>(type: "jsonb", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    address_line1 = table.Column<string>(type: "text", nullable: true),
                    address_line2 = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: true),
                    zip = table.Column<string>(type: "text", nullable: true),
                    logo_images_id = table.Column<Guid>(type: "uuid", nullable: true),
                    brand_primary = table.Column<string>(type: "text", nullable: true),
                    brand_secondary = table.Column<string>(type: "text", nullable: true),
                    brand_accent = table.Column<string>(type: "text", nullable: true),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    default_fee_formulas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gateway_fee_formulas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.tenants_id);
                    table.ForeignKey(
                        name: "fk_tenants_fee_formulas_default_fee_formulas_id",
                        column: x => x.default_fee_formulas_id,
                        principalTable: "fee_formulas",
                        principalColumn: "fee_formulas_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tenants_fee_formulas_gateway_fee_formulas_id",
                        column: x => x.gateway_fee_formulas_id,
                        principalTable: "fee_formulas",
                        principalColumn: "fee_formulas_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "floor_plan_templates",
                columns: table => new
                {
                    floor_plan_templates_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floor_plan_templates", x => x.floor_plan_templates_id);
                    table.ForeignKey(
                        name: "fk_floor_plan_templates_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "images",
                columns: table => new
                {
                    images_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by_users_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploader_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    alt_text = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    caption = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Generic"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_images", x => x.images_id);
                    table.ForeignKey(
                        name: "fk_images_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "performers",
                columns: table => new
                {
                    performers_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    primary_image_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    meta = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_performers", x => x.performers_id);
                    table.ForeignKey(
                        name: "fk_performers_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sponsors",
                columns: table => new
                {
                    sponsors_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    primary_image_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    meta = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsors", x => x.sponsors_id);
                    table.ForeignKey(
                        name: "fk_sponsors_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stripe_payouts",
                columns: table => new
                {
                    stripe_payouts_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    stripe_payout_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_cents = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "usd"),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    arrival_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    raw_event = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stripe_payouts", x => x.stripe_payouts_id);
                    table.ForeignKey(
                        name: "fk_stripe_payouts_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "table_templates",
                columns: table => new
                {
                    table_templates_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    default_capacity = table.Column<int>(type: "integer", nullable: false),
                    default_shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    default_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    default_price_cents = table.Column<int>(type: "integer", nullable: false),
                    default_width = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 80m),
                    default_height = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 80m),
                    default_is_all_inclusive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_table_templates", x => x.table_templates_id);
                    table.CheckConstraint("CK_table_templates_DefaultCapacity", "default_capacity > 0");
                    table.CheckConstraint("CK_table_templates_DefaultPriceCents", "default_price_cents >= 0");
                    table.CheckConstraint("CK_table_templates_DefaultShape", "default_shape IN ('Round','Rectangle','Square','Cocktail')");
                    table.ForeignKey(
                        name: "fk_table_templates_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_stripe_profiles",
                columns: table => new
                {
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    business_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    product_description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    mcc = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    support_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_stripe_profiles", x => x.tenants_id);
                    table.ForeignKey(
                        name: "fk_tenant_stripe_profiles_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    venues_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    image_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    website = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    addresses_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venues", x => x.venues_id);
                    table.ForeignKey(
                        name: "fk_venues_addresses_addresses_id",
                        column: x => x.addresses_id,
                        principalTable: "addresses",
                        principalColumn: "addresses_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_venues_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "floor_plan_template_objects",
                columns: table => new
                {
                    floor_plan_template_objects_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor_plan_templates_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    pos_x = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    pos_y = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    width = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 80m),
                    height = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 80m),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floor_plan_template_objects", x => x.floor_plan_template_objects_id);
                    table.CheckConstraint("CK_fpt_objects_ObjectType", "object_type IN ('Entry','Exit','Stage')");
                    table.ForeignKey(
                        name: "fk_floor_plan_template_objects_floor_plan_templates_floor_plan",
                        column: x => x.floor_plan_templates_id,
                        principalTable: "floor_plan_templates",
                        principalColumn: "floor_plan_templates_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_floor_plan_template_objects_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "floor_plan_template_tables",
                columns: table => new
                {
                    floor_plan_template_tables_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor_plan_templates_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    type_label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    pos_x = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    pos_y = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    width = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 80m),
                    height = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 80m),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floor_plan_template_tables", x => x.floor_plan_template_tables_id);
                    table.CheckConstraint("CK_fpt_tables_Shape", "shape IN ('Round','Rectangle','Square','Cocktail')");
                    table.ForeignKey(
                        name: "fk_floor_plan_template_tables_floor_plan_templates_floor_plan_",
                        column: x => x.floor_plan_templates_id,
                        principalTable: "floor_plan_templates",
                        principalColumn: "floor_plan_templates_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_floor_plan_template_tables_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_images",
                columns: table => new
                {
                    platform_images_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    images_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "users",
                columns: table => new
                {
                    users_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    first_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    last_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    pepper_version = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    google_subject = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    role = table.Column<short>(type: "smallint", nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    images_id = table.Column<Guid>(type: "uuid", nullable: true),
                    addresses_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    email_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_request_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    opt_in_location_email = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    has_completed_onboarding = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.users_id);
                    table.CheckConstraint("CK_users_DeveloperHasNoTenant", "(role = 99) = (tenants_id IS NULL)");
                    table.CheckConstraint("CK_users_Role", "role IN (0, 1, 2, 3, 99)");
                    table.ForeignKey(
                        name: "fk_users_addresses_addresses_id",
                        column: x => x.addresses_id,
                        principalTable: "addresses",
                        principalColumn: "addresses_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_users_images_images_id",
                        column: x => x.images_id,
                        principalTable: "images",
                        principalColumn: "images_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_users_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "table_template_price_rules",
                columns: table => new
                {
                    table_template_price_rules_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_templates_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rule_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    active_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    active_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    min_remaining = table.Column<int>(type: "integer", nullable: true),
                    max_remaining = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_table_template_price_rules", x => x.table_template_price_rules_id);
                    table.CheckConstraint("CK_table_template_price_rules_PriceCents", "price_cents >= 0");
                    table.CheckConstraint("CK_table_template_price_rules_RuleType", "rule_type IN ('Presale','LastMinute','TimeWindow','Dynamic')");
                    table.CheckConstraint("CK_table_template_price_rules_Window", "active_from IS NULL OR active_until IS NULL OR active_until > active_from");
                    table.ForeignKey(
                        name: "fk_table_template_price_rules_table_templates_table_templates_",
                        column: x => x.table_templates_id,
                        principalTable: "table_templates",
                        principalColumn: "table_templates_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_table_template_price_rules_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "venue_images",
                columns: table => new
                {
                    venue_images_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venues_id = table.Column<Guid>(type: "uuid", nullable: false),
                    images_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venue_images", x => x.venue_images_id);
                    table.ForeignKey(
                        name: "fk_venue_images_images_images_id",
                        column: x => x.images_id,
                        principalTable: "images",
                        principalColumn: "images_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_venue_images_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_venue_images_venues_venues_id",
                        column: x => x.venues_id,
                        principalTable: "venues",
                        principalColumn: "venues_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_sessions",
                columns: table => new
                {
                    device_sessions_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    users_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    device_fingerprint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    device_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_sessions", x => x.device_sessions_id);
                    table.ForeignKey(
                        name: "fk_device_sessions_users_users_id",
                        column: x => x.users_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    events_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    slug = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    image_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    is_featured = table.Column<bool>(type: "boolean", nullable: false),
                    layout_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    event_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    scheduled_publish_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fees_included = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    meta = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "title", "description" }),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venues_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_users_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.events_id);
                    table.CheckConstraint("CK_events_Category", "category IS NULL OR category IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports')");
                    table.CheckConstraint("CK_events_CompletedRequiresPublish", "status <> 'Completed' OR published_at IS NOT NULL");
                    table.CheckConstraint("CK_events_DateRange", "end_date > start_date");
                    table.CheckConstraint("CK_events_DraftNoPublishDate", "status <> 'Draft' OR published_at IS NULL");
                    table.CheckConstraint("CK_events_EventType", "event_type IN ('Open','Table','Both')");
                    table.CheckConstraint("CK_events_LayoutMode", "layout_mode IN ('Grid','Open')");
                    table.CheckConstraint("CK_events_PublishLifecycle", "status <> 'Published' OR published_at IS NOT NULL");
                    table.CheckConstraint("CK_events_Status", "status IN ('Draft','Published','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "fk_events_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_events_users_created_by_users_id",
                        column: x => x.created_by_users_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_events_venues_venues_id",
                        column: x => x.venues_id,
                        principalTable: "venues",
                        principalColumn: "venues_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "feedbacks",
                columns: table => new
                {
                    feedbacks_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    users_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    diagnostics = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feedbacks", x => x.feedbacks_id);
                    table.ForeignKey(
                        name: "fk_feedbacks_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_feedbacks_users_users_id",
                        column: x => x.users_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    password_reset_tokens_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    users_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_reset_tokens", x => x.password_reset_tokens_id);
                    table.CheckConstraint("CK_password_reset_tokens_Usage", "(is_used = false AND used_at IS NULL) OR (is_used = true AND used_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_password_reset_tokens_users_users_id",
                        column: x => x.users_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_email_verification_tokens",
                columns: table => new
                {
                    user_email_verification_tokens_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    users_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    bookings_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    users_id = table.Column<Guid>(type: "uuid", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subtotal_cents = table.Column<int>(type: "integer", nullable: false),
                    fee_cents = table.Column<int>(type: "integer", nullable: false),
                    total_cents = table.Column<int>(type: "integer", nullable: false),
                    qr_token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    hold_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    seats_reserved = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.bookings_id);
                    table.CheckConstraint("CK_bookings_FeeCents", "fee_cents >= 0");
                    table.CheckConstraint("CK_bookings_SeatsReserved", "seats_reserved IS NULL OR seats_reserved > 0");
                    table.CheckConstraint("CK_bookings_Status", "status IN ('Pending','Paid','CheckedIn','Cancelled','Refunded','Expired')");
                    table.CheckConstraint("CK_bookings_SubtotalCents", "subtotal_cents >= 0");
                    table.CheckConstraint("CK_bookings_TotalCents", "total_cents >= 0");
                    table.CheckConstraint("CK_bookings_TotalFormula", "total_cents = subtotal_cents + fee_cents");
                    table.ForeignKey(
                        name: "fk_bookings_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_users_users_id",
                        column: x => x.users_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_images",
                columns: table => new
                {
                    event_images_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    images_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "event_image"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_images", x => x.event_images_id);
                    table.CheckConstraint("CK_event_images_Type", "type IN ('event_image','event_thumbnail')");
                    table.ForeignKey(
                        name: "fk_event_images_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_images_images_images_id",
                        column: x => x.images_id,
                        principalTable: "images",
                        principalColumn: "images_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_images_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_performers",
                columns: table => new
                {
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performers_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    event_meta = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_performers", x => new { x.events_id, x.performers_id });
                    table.ForeignKey(
                        name: "fk_event_performers_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_performers_performers_performers_id",
                        column: x => x.performers_id,
                        principalTable: "performers",
                        principalColumn: "performers_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_performers_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_sponsors",
                columns: table => new
                {
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sponsors_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    event_meta = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_sponsors", x => new { x.events_id, x.sponsors_id });
                    table.ForeignKey(
                        name: "fk_event_sponsors_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_sponsors_sponsors_sponsors_id",
                        column: x => x.sponsors_id,
                        principalTable: "sponsors",
                        principalColumn: "sponsors_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_sponsors_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                columns: table => new
                {
                    invitations_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    role = table.Column<short>(type: "smallint", nullable: false),
                    invited_by_users_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.invitations_id);
                    table.CheckConstraint("CK_invitations_Role", "role IN (1, 2, 3, 99)");
                    table.CheckConstraint("CK_invitations_Status", "status IN ('Pending','Accepted','Revoked','Expired')");
                    table.ForeignKey(
                        name: "fk_invitations_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invitations_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invitations_users_invited_by_users_id",
                        column: x => x.invited_by_users_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "layout_objects",
                columns: table => new
                {
                    layout_objects_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    pos_x = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    pos_y = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    width = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 80m),
                    height = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 80m),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_layout_objects", x => x.layout_objects_id);
                    table.CheckConstraint("CK_layout_objects_ObjectType", "object_type IN ('Entry','Exit','Stage')");
                    table.CheckConstraint("CK_layout_objects_PosX", "pos_x >= 0");
                    table.CheckConstraint("CK_layout_objects_PosY", "pos_y >= 0");
                    table.ForeignKey(
                        name: "fk_layout_objects_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_layout_objects_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prices",
                columns: table => new
                {
                    prices_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    pricing_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    base_price_cents = table.Column<int>(type: "integer", nullable: false),
                    per_attendee_cents = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_all_inclusive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    fee_formulas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    max_quantity = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prices", x => x.prices_id);
                    table.CheckConstraint("CK_prices_BasePriceCents", "base_price_cents >= 0");
                    table.CheckConstraint("CK_prices_MaxQuantity", "max_quantity IS NULL OR max_quantity > 0");
                    table.CheckConstraint("CK_prices_PerAttendeeCents", "per_attendee_cents >= 0");
                    table.CheckConstraint("CK_prices_PricingType", "pricing_type IN ('TicketTier','Table')");
                    table.ForeignKey(
                        name: "fk_prices_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_prices_fee_formulas_fee_formulas_id",
                        column: x => x.fee_formulas_id,
                        principalTable: "fee_formulas",
                        principalColumn: "fee_formulas_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_prices_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schedule_items",
                columns: table => new
                {
                    schedule_items_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    type_category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_items", x => x.schedule_items_id);
                    table.CheckConstraint("CK_schedule_items_TimeRange", "end_time > start_time");
                    table.CheckConstraint("CK_schedule_items_TypeCategory", "type_category IN ('Performance','Break','Intermission','DJ Set','Networking','Other')");
                    table.ForeignKey(
                        name: "fk_schedule_items_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_schedule_items_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_event_access",
                columns: table => new
                {
                    staff_event_access_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_event_access", x => x.staff_event_access_id);
                    table.ForeignKey(
                        name: "fk_staff_event_access_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_staff_event_access_users_assigned_by_admin_id",
                        column: x => x.assigned_by_admin_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_staff_event_access_users_staff_user_id",
                        column: x => x.staff_user_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stripe_transactions",
                columns: table => new
                {
                    stripe_transactions_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bookings_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_intent_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_cents = table.Column<int>(type: "integer", nullable: false),
                    transfer_amount_cents = table.Column<int>(type: "integer", nullable: true),
                    total_charged_cents = table.Column<int>(type: "integer", nullable: true),
                    stripe_fees_cents = table.Column<int>(type: "integer", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refund_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    refunded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stripe_transactions", x => x.stripe_transactions_id);
                    table.CheckConstraint("CK_stripe_transactions_AmountCents", "amount_cents >= 0");
                    table.CheckConstraint("CK_stripe_transactions_Currency", "currency IN ('usd')");
                    table.CheckConstraint("CK_stripe_transactions_NotRefundedNoRefundDate", "status = 'Refunded' OR refunded_at IS NULL");
                    table.CheckConstraint("CK_stripe_transactions_PaidLifecycle", "status NOT IN ('Succeeded','Refunded') OR paid_at IS NOT NULL");
                    table.CheckConstraint("CK_stripe_transactions_PendingNoPaidDate", "status NOT IN ('RequiresConfirmation','Failed') OR paid_at IS NULL");
                    table.CheckConstraint("CK_stripe_transactions_RefundLifecycle", "status <> 'Refunded' OR refunded_at IS NOT NULL");
                    table.CheckConstraint("CK_stripe_transactions_Status", "status IN ('RequiresConfirmation','Succeeded','Failed','Refunded')");
                    table.CheckConstraint("CK_stripe_transactions_StripeFees", "stripe_fees_cents IS NULL OR stripe_fees_cents >= 0");
                    table.CheckConstraint("CK_stripe_transactions_TotalCharged", "total_charged_cents IS NULL OR total_charged_cents >= 0");
                    table.CheckConstraint("CK_stripe_transactions_TransferAmount", "transfer_amount_cents IS NULL OR transfer_amount_cents >= 0");
                    table.ForeignKey(
                        name: "fk_stripe_transactions_bookings_bookings_id",
                        column: x => x.bookings_id,
                        principalTable: "bookings",
                        principalColumn: "bookings_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stripe_transactions_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stripe_transfers",
                columns: table => new
                {
                    stripe_transfers_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    stripe_transfer_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bookings_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_cents = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "usd"),
                    raw_event = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stripe_transfers", x => x.stripe_transfers_id);
                    table.ForeignKey(
                        name: "fk_stripe_transfers_bookings_bookings_id",
                        column: x => x.bookings_id,
                        principalTable: "bookings",
                        principalColumn: "bookings_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_stripe_transfers_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_tables",
                columns: table => new
                {
                    event_tables_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    platform_fee_cents = table.Column<int>(type: "integer", nullable: true),
                    fee_formulas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_width = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    default_height = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_templates_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prices_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_tables", x => x.event_tables_id);
                    table.CheckConstraint("CK_event_tables_Capacity", "capacity > 0");
                    table.CheckConstraint("CK_event_tables_PriceCents", "price_cents >= 0");
                    table.CheckConstraint("CK_event_tables_Shape", "shape IN ('Round','Rectangle','Square','Cocktail')");
                    table.ForeignKey(
                        name: "fk_event_tables_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_tables_fee_formulas_fee_formulas_id",
                        column: x => x.fee_formulas_id,
                        principalTable: "fee_formulas",
                        principalColumn: "fee_formulas_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_event_tables_prices_prices_id",
                        column: x => x.prices_id,
                        principalTable: "prices",
                        principalColumn: "prices_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_event_tables_table_templates_table_templates_id",
                        column: x => x.table_templates_id,
                        principalTable: "table_templates",
                        principalColumn: "table_templates_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_event_tables_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_ticket_types",
                columns: table => new
                {
                    event_ticket_types_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    platform_fee_cents = table.Column<int>(type: "integer", nullable: true),
                    fee_formulas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    max_quantity = table.Column<int>(type: "integer", nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prices_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_ticket_types", x => x.event_ticket_types_id);
                    table.CheckConstraint("CK_event_ticket_types_Capacity", "capacity IS NULL OR capacity > 0");
                    table.CheckConstraint("CK_event_ticket_types_MaxQuantity", "max_quantity IS NULL OR max_quantity > 0");
                    table.CheckConstraint("CK_event_ticket_types_PriceCents", "price_cents >= 0");
                    table.CheckConstraint("CK_event_ticket_types_SortOrder", "sort_order >= 0");
                    table.ForeignKey(
                        name: "fk_event_ticket_types_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_ticket_types_fee_formulas_fee_formulas_id",
                        column: x => x.fee_formulas_id,
                        principalTable: "fee_formulas",
                        principalColumn: "fee_formulas_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_event_ticket_types_prices_prices_id",
                        column: x => x.prices_id,
                        principalTable: "prices",
                        principalColumn: "prices_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_event_ticket_types_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_rules",
                columns: table => new
                {
                    price_rules_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Price"),
                    prices_id = table.Column<Guid>(type: "uuid", nullable: true),
                    events_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rule_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    active_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    active_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    min_remaining = table.Column<int>(type: "integer", nullable: true),
                    max_remaining = table.Column<int>(type: "integer", nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_rules", x => x.price_rules_id);
                    table.CheckConstraint("CK_price_rules_Capacity", "capacity IS NULL OR capacity > 0");
                    table.CheckConstraint("CK_price_rules_PriceCents", "price_cents >= 0");
                    table.CheckConstraint("CK_price_rules_RuleType", "rule_type IN ('Presale','LastMinute','TimeWindow','Dynamic')");
                    table.CheckConstraint("CK_price_rules_Scope", "(scope = 'Price' AND prices_id IS NOT NULL AND events_id IS NULL) OR (scope = 'Event' AND events_id IS NOT NULL AND prices_id IS NULL)");
                    table.CheckConstraint("CK_price_rules_Window", "active_from IS NULL OR active_until IS NULL OR active_until > active_from");
                    table.ForeignKey(
                        name: "fk_price_rules_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_price_rules_prices_prices_id",
                        column: x => x.prices_id,
                        principalTable: "prices",
                        principalColumn: "prices_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_price_rules_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tables",
                columns: table => new
                {
                    tables_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pos_x = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    pos_y = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    width = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 80m),
                    height = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 80m),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    shape_override = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    color_override = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    capacity_override = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Available"),
                    locked_by_users_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lock_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    event_tables_id = table.Column<Guid>(type: "uuid", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tables", x => x.tables_id);
                    table.CheckConstraint("CK_tables_AvailableNoLock", "status <> 'Available' OR (locked_by_users_id IS NULL AND lock_expires_at IS NULL)");
                    table.CheckConstraint("CK_tables_LockedRequiresOwner", "status <> 'Locked' OR (locked_by_users_id IS NOT NULL AND lock_expires_at IS NOT NULL)");
                    table.CheckConstraint("CK_tables_PosX", "pos_x >= 0");
                    table.CheckConstraint("CK_tables_PosY", "pos_y >= 0");
                    table.CheckConstraint("CK_tables_Status", "status IN ('Available','Locked','Booked')");
                    table.ForeignKey(
                        name: "fk_tables_event_tables_event_tables_id",
                        column: x => x.event_tables_id,
                        principalTable: "event_tables",
                        principalColumn: "event_tables_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tables_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tables_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tables_users_locked_by_users_id",
                        column: x => x.locked_by_users_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "booking_lines",
                columns: table => new
                {
                    booking_lines_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenants_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bookings_id = table.Column<Guid>(type: "uuid", nullable: false),
                    events_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    event_ticket_types_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tables_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prices_id = table.Column<Guid>(type: "uuid", nullable: true),
                    seats = table.Column<int>(type: "integer", nullable: false),
                    ticket_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    qr_token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    seat_number = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    guest_users_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invite_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    invite_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    invited_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    invite_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    base_price_cents = table.Column<int>(type: "integer", nullable: false),
                    selling_price_cents = table.Column<int>(type: "integer", nullable: false),
                    discount_cents = table.Column<int>(type: "integer", nullable: false),
                    applied_price_rules_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_rule_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    platform_fee_cents = table.Column<int>(type: "integer", nullable: false),
                    gateway_fee_cents = table.Column<int>(type: "integer", nullable: false),
                    final_price_cents = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "usd"),
                    subtotal_cents = table.Column<int>(type: "integer", nullable: false),
                    fee_cents = table.Column<int>(type: "integer", nullable: false),
                    total_cents = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_lines", x => x.booking_lines_id);
                    table.CheckConstraint("CK_booking_lines_FeeCents", "fee_cents >= 0");
                    table.CheckConstraint("CK_booking_lines_Kind", "kind IN ('Ticket','Table')");
                    table.CheckConstraint("CK_booking_lines_Ref", "(kind = 'Ticket' AND event_ticket_types_id IS NOT NULL AND tables_id IS NULL) OR (kind = 'Table' AND tables_id IS NOT NULL AND event_ticket_types_id IS NULL)");
                    table.CheckConstraint("CK_booking_lines_Seats", "seats > 0");
                    table.CheckConstraint("CK_booking_lines_SubtotalCents", "subtotal_cents >= 0");
                    table.CheckConstraint("CK_booking_lines_TicketStatus", "status IN ('Unassigned','Invited','Claimed','CheckedIn')");
                    table.CheckConstraint("CK_booking_lines_TotalFormula", "total_cents = subtotal_cents + fee_cents");
                    table.ForeignKey(
                        name: "fk_booking_lines_bookings_bookings_id",
                        column: x => x.bookings_id,
                        principalTable: "bookings",
                        principalColumn: "bookings_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_booking_lines_event_ticket_types_event_ticket_types_id",
                        column: x => x.event_ticket_types_id,
                        principalTable: "event_ticket_types",
                        principalColumn: "event_ticket_types_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_booking_lines_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_booking_lines_prices_prices_id",
                        column: x => x.prices_id,
                        principalTable: "prices",
                        principalColumn: "prices_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_booking_lines_tables_tables_id",
                        column: x => x.tables_id,
                        principalTable: "tables",
                        principalColumn: "tables_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_booking_lines_tenants_tenants_id",
                        column: x => x.tenants_id,
                        principalTable: "tenants",
                        principalColumn: "tenants_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_booking_lines_users_guest_users_id",
                        column: x => x.guest_users_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "checkin_logs",
                columns: table => new
                {
                    checkin_logs_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_checkin_logs", x => x.checkin_logs_id);
                    table.ForeignKey(
                        name: "fk_checkin_logs_booking_lines_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "booking_lines",
                        principalColumn: "booking_lines_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_checkin_logs_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "bookings_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_checkin_logs_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "events_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_checkin_logs_users_staff_user_id",
                        column: x => x.staff_user_id,
                        principalTable: "users",
                        principalColumn: "users_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_settings_key",
                table: "app_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_type_actor_id_created_at",
                table: "audit_logs",
                columns: new[] { "actor_type", "actor_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_subject_type_subject_id_created_at",
                table: "audit_logs",
                columns: new[] { "subject_type", "subject_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenants_id",
                table: "audit_logs",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_bookings_id",
                table: "booking_lines",
                column: "bookings_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_bookings_id_seat_number",
                table: "booking_lines",
                columns: new[] { "bookings_id", "seat_number" },
                unique: true,
                filter: "seat_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_event_ticket_types_id",
                table: "booking_lines",
                column: "event_ticket_types_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_events_id",
                table: "booking_lines",
                column: "events_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_events_id_ticket_code",
                table: "booking_lines",
                columns: new[] { "events_id", "ticket_code" },
                unique: true,
                filter: "ticket_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_guest_users_id",
                table: "booking_lines",
                column: "guest_users_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_invite_token_hash",
                table: "booking_lines",
                column: "invite_token_hash",
                unique: true,
                filter: "invite_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_prices_id",
                table: "booking_lines",
                column: "prices_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_qr_token",
                table: "booking_lines",
                column: "qr_token",
                unique: true,
                filter: "qr_token IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_tables_id",
                table: "booking_lines",
                column: "tables_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_tenants_id",
                table: "booking_lines",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_events_id_status",
                table: "bookings",
                columns: new[] { "events_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_events_id_users_id_booking_number",
                table: "bookings",
                columns: new[] { "events_id", "users_id", "booking_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_qr_token",
                table: "bookings",
                column: "qr_token",
                unique: true,
                filter: "qr_token IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_status",
                table: "bookings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_tenants_id",
                table: "bookings",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_users_id",
                table: "bookings",
                column: "users_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_users_id_created_at",
                table: "bookings",
                columns: new[] { "users_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_checkin_logs_booking_id",
                table: "checkin_logs",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_checkin_logs_event_id",
                table: "checkin_logs",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_checkin_logs_staff_user_id",
                table: "checkin_logs",
                column: "staff_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_checkin_logs_ticket_id",
                table: "checkin_logs",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_sessions_expires_at_revoked_at",
                table: "device_sessions",
                columns: new[] { "expires_at", "revoked_at" },
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_device_sessions_session_hash",
                table: "device_sessions",
                column: "session_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_sessions_users_id",
                table: "device_sessions",
                column: "users_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_logs_tenants_id",
                table: "email_logs",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_logs_timestamp",
                table: "email_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_enum_definitions_enum_type_enum_value",
                table: "enum_definitions",
                columns: new[] { "enum_type", "enum_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_images_events_id_images_id",
                table: "event_images",
                columns: new[] { "events_id", "images_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_images_events_id_sort_order",
                table: "event_images",
                columns: new[] { "events_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_event_images_events_id_type",
                table: "event_images",
                columns: new[] { "events_id", "type" },
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "ix_event_images_images_id",
                table: "event_images",
                column: "images_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_images_tenants_id",
                table: "event_images",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_performers_events_id_sort_order",
                table: "event_performers",
                columns: new[] { "events_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_event_performers_performers_id",
                table: "event_performers",
                column: "performers_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_performers_tenants_id",
                table: "event_performers",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_sponsors_events_id_sort_order",
                table: "event_sponsors",
                columns: new[] { "events_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_event_sponsors_sponsors_id",
                table: "event_sponsors",
                column: "sponsors_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_sponsors_tenants_id",
                table: "event_sponsors",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_tables_events_id_label",
                table: "event_tables",
                columns: new[] { "events_id", "label" });

            migrationBuilder.CreateIndex(
                name: "ix_event_tables_fee_formulas_id",
                table: "event_tables",
                column: "fee_formulas_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_tables_prices_id",
                table: "event_tables",
                column: "prices_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_tables_table_templates_id",
                table: "event_tables",
                column: "table_templates_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_tables_tenants_id",
                table: "event_tables",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_ticket_types_events_id_label",
                table: "event_ticket_types",
                columns: new[] { "events_id", "label" });

            migrationBuilder.CreateIndex(
                name: "ix_event_ticket_types_events_id_sort_order",
                table: "event_ticket_types",
                columns: new[] { "events_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_event_ticket_types_fee_formulas_id",
                table: "event_ticket_types",
                column: "fee_formulas_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_ticket_types_prices_id",
                table: "event_ticket_types",
                column: "prices_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_ticket_types_tenants_id",
                table: "event_ticket_types",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_category",
                table: "events",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_events_created_by_users_id",
                table: "events",
                column: "created_by_users_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_search_vector",
                table: "events",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_events_slug",
                table: "events",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_start_date",
                table: "events",
                column: "start_date");

            migrationBuilder.CreateIndex(
                name: "ix_events_status",
                table: "events",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_events_status_start_date",
                table: "events",
                columns: new[] { "status", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_events_tenants_id",
                table: "events",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_venues_id",
                table: "events",
                column: "venues_id");

            migrationBuilder.CreateIndex(
                name: "ix_feedbacks_created_at",
                table: "feedbacks",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_feedbacks_tenants_id",
                table: "feedbacks",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_feedbacks_type",
                table: "feedbacks",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_feedbacks_users_id",
                table: "feedbacks",
                column: "users_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plan_template_objects_floor_plan_templates_id",
                table: "floor_plan_template_objects",
                column: "floor_plan_templates_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plan_template_objects_tenants_id",
                table: "floor_plan_template_objects",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plan_template_tables_floor_plan_templates_id",
                table: "floor_plan_template_tables",
                column: "floor_plan_templates_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plan_template_tables_tenants_id",
                table: "floor_plan_template_tables",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plan_templates_tenants_id",
                table: "floor_plan_templates",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_images_entity_type_entity_id",
                table: "images",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_images_tenants_id",
                table: "images",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_email",
                table: "invitations",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_event_id",
                table: "invitations",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_invited_by_users_id",
                table: "invitations",
                column: "invited_by_users_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_tenants_id",
                table: "invitations",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_token_hash",
                table: "invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_layout_objects_events_id",
                table: "layout_objects",
                column: "events_id");

            migrationBuilder.CreateIndex(
                name: "ix_layout_objects_tenants_id",
                table: "layout_objects",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_magic_link_tokens_email",
                table: "magic_link_tokens",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_magic_link_tokens_expires_at",
                table: "magic_link_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_magic_link_tokens_token_hash",
                table: "magic_link_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_expires_at",
                table: "password_reset_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_users_id",
                table: "password_reset_tokens",
                column: "users_id");

            migrationBuilder.CreateIndex(
                name: "ix_performers_name",
                table: "performers",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_performers_slug",
                table: "performers",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_performers_tenants_id",
                table: "performers",
                column: "tenants_id");

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
                name: "ix_price_rules_events_id_scope_priority",
                table: "price_rules",
                columns: new[] { "events_id", "scope", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_price_rules_prices_id_priority",
                table: "price_rules",
                columns: new[] { "prices_id", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_price_rules_tenants_id",
                table: "price_rules",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_prices_events_id",
                table: "prices",
                column: "events_id");

            migrationBuilder.CreateIndex(
                name: "ix_prices_fee_formulas_id",
                table: "prices",
                column: "fee_formulas_id");

            migrationBuilder.CreateIndex(
                name: "ix_prices_tenants_id",
                table: "prices",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_items_events_id_start_time",
                table: "schedule_items",
                columns: new[] { "events_id", "start_time" });

            migrationBuilder.CreateIndex(
                name: "ix_schedule_items_tenants_id",
                table: "schedule_items",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsors_name",
                table: "sponsors",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_sponsors_slug",
                table: "sponsors",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sponsors_tenants_id",
                table: "sponsors",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_event_access_assigned_by_admin_id",
                table: "staff_event_access",
                column: "assigned_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_event_access_event_id",
                table: "staff_event_access",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_event_access_staff_user_id_event_id",
                table: "staff_event_access",
                columns: new[] { "staff_user_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stripe_payouts_stripe_payout_id",
                table: "stripe_payouts",
                column: "stripe_payout_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stripe_payouts_tenants_id",
                table: "stripe_payouts",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_stripe_transactions_bookings_id",
                table: "stripe_transactions",
                column: "bookings_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stripe_transactions_payment_intent_id",
                table: "stripe_transactions",
                column: "payment_intent_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stripe_transactions_status_paid_at",
                table: "stripe_transactions",
                columns: new[] { "status", "paid_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stripe_transactions_tenants_id",
                table: "stripe_transactions",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_stripe_transfers_bookings_id",
                table: "stripe_transfers",
                column: "bookings_id");

            migrationBuilder.CreateIndex(
                name: "ix_stripe_transfers_stripe_transfer_id",
                table: "stripe_transfers",
                column: "stripe_transfer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stripe_transfers_tenants_id",
                table: "stripe_transfers",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_table_template_price_rules_table_templates_id_priority",
                table: "table_template_price_rules",
                columns: new[] { "table_templates_id", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_table_template_price_rules_tenants_id",
                table: "table_template_price_rules",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_table_templates_tenants_id",
                table: "table_templates",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_table_templates_tenants_id_default_color",
                table: "table_templates",
                columns: new[] { "tenants_id", "default_color" },
                unique: true,
                filter: "default_color IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_table_templates_tenants_id_name",
                table: "table_templates",
                columns: new[] { "tenants_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tables_event_tables_id",
                table: "tables",
                column: "event_tables_id");

            migrationBuilder.CreateIndex(
                name: "ix_tables_events_id",
                table: "tables",
                column: "events_id");

            migrationBuilder.CreateIndex(
                name: "ix_tables_events_id_label",
                table: "tables",
                columns: new[] { "events_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tables_events_id_status",
                table: "tables",
                columns: new[] { "events_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tables_locked_by_users_id",
                table: "tables",
                column: "locked_by_users_id");

            migrationBuilder.CreateIndex(
                name: "ix_tables_tenants_id",
                table: "tables",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_archived_at",
                table: "tenants",
                column: "archived_at");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_default_fee_formulas_id",
                table: "tenants",
                column: "default_fee_formulas_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_gateway_fee_formulas_id",
                table: "tenants",
                column: "gateway_fee_formulas_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_stripe_connected_account_id",
                table: "tenants",
                column: "stripe_connected_account_id",
                unique: true,
                filter: "stripe_connected_account_id IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "ix_users_addresses_id",
                table: "users",
                column: "addresses_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email_hash",
                table: "users",
                column: "email_hash");

            migrationBuilder.CreateIndex(
                name: "ix_users_google_subject",
                table: "users",
                column: "google_subject",
                unique: true,
                filter: "google_subject IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_images_id",
                table: "users",
                column: "images_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_tenants_id_email_role",
                table: "users",
                columns: new[] { "tenants_id", "email", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_venue_images_images_id",
                table: "venue_images",
                column: "images_id");

            migrationBuilder.CreateIndex(
                name: "ix_venue_images_tenants_id",
                table: "venue_images",
                column: "tenants_id");

            migrationBuilder.CreateIndex(
                name: "ix_venue_images_venues_id",
                table: "venue_images",
                column: "venues_id",
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "ix_venue_images_venues_id_images_id",
                table: "venue_images",
                columns: new[] { "venues_id", "images_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_venue_images_venues_id_sort_order",
                table: "venue_images",
                columns: new[] { "venues_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_venues_addresses_id",
                table: "venues",
                column: "addresses_id");

            migrationBuilder.CreateIndex(
                name: "ix_venues_name",
                table: "venues",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_venues_tenants_id",
                table: "venues",
                column: "tenants_id");

            // SQL objects loading deferred to the final migration (AddSalesTax)
        }

        
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "checkin_logs");

            migrationBuilder.DropTable(
                name: "device_sessions");

            migrationBuilder.DropTable(
                name: "email_logs");

            migrationBuilder.DropTable(
                name: "enum_definitions");

            migrationBuilder.DropTable(
                name: "event_images");

            migrationBuilder.DropTable(
                name: "event_performers");

            migrationBuilder.DropTable(
                name: "event_sponsors");

            migrationBuilder.DropTable(
                name: "feedbacks");

            migrationBuilder.DropTable(
                name: "floor_plan_template_objects");

            migrationBuilder.DropTable(
                name: "floor_plan_template_tables");

            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "layout_objects");

            migrationBuilder.DropTable(
                name: "magic_link_tokens");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "platform_images");

            migrationBuilder.DropTable(
                name: "price_rules");

            migrationBuilder.DropTable(
                name: "schedule_items");

            migrationBuilder.DropTable(
                name: "staff_event_access");

            migrationBuilder.DropTable(
                name: "stripe_payouts");

            migrationBuilder.DropTable(
                name: "stripe_transactions");

            migrationBuilder.DropTable(
                name: "stripe_transfers");

            migrationBuilder.DropTable(
                name: "table_template_price_rules");

            migrationBuilder.DropTable(
                name: "tenant_stripe_profiles");

            migrationBuilder.DropTable(
                name: "user_email_verification_tokens");

            migrationBuilder.DropTable(
                name: "venue_images");

            migrationBuilder.DropTable(
                name: "booking_lines");

            migrationBuilder.DropTable(
                name: "performers");

            migrationBuilder.DropTable(
                name: "sponsors");

            migrationBuilder.DropTable(
                name: "floor_plan_templates");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "event_ticket_types");

            migrationBuilder.DropTable(
                name: "tables");

            migrationBuilder.DropTable(
                name: "event_tables");

            migrationBuilder.DropTable(
                name: "prices");

            migrationBuilder.DropTable(
                name: "table_templates");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "venues");

            migrationBuilder.DropTable(
                name: "images");

            migrationBuilder.DropTable(
                name: "addresses");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "fee_formulas");
        }
    }
}
