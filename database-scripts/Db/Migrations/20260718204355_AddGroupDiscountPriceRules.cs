using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{

    public partial class AddGroupDiscountPriceRules : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_price_rules_RuleType",
                table: "price_rules");

            migrationBuilder.AddColumn<int>(
                name: "discount_bps",
                table: "price_rules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discount_kind",
                table: "price_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_qty",
                table: "price_rules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "min_qty",
                table: "price_rules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_price_rules_DiscountBps",
                table: "price_rules",
                sql: "discount_bps IS NULL OR (discount_bps > 0 AND discount_bps <= 10000)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_price_rules_DiscountKind",
                table: "price_rules",
                sql: "discount_kind IS NULL OR discount_kind IN ('FixedUnitPrice','PercentOff','AmountOffOrder')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_price_rules_GroupShape",
                table: "price_rules",
                sql: "rule_type <> 'Group' OR (min_qty IS NOT NULL AND discount_kind IS NOT NULL AND (discount_kind <> 'PercentOff' OR discount_bps IS NOT NULL) AND (discount_kind <> 'AmountOffOrder' OR scope = 'Event'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_price_rules_MinQty",
                table: "price_rules",
                sql: "min_qty IS NULL OR min_qty > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_price_rules_QtyRange",
                table: "price_rules",
                sql: "min_qty IS NULL OR max_qty IS NULL OR max_qty >= min_qty");

            migrationBuilder.AddCheckConstraint(
                name: "CK_price_rules_RuleType",
                table: "price_rules",
                sql: "rule_type IN ('Presale','LastMinute','TimeWindow','Dynamic','Group')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_price_rules_DiscountBps",
                table: "price_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_price_rules_DiscountKind",
                table: "price_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_price_rules_GroupShape",
                table: "price_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_price_rules_MinQty",
                table: "price_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_price_rules_QtyRange",
                table: "price_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_price_rules_RuleType",
                table: "price_rules");

            migrationBuilder.DropColumn(
                name: "discount_bps",
                table: "price_rules");

            migrationBuilder.DropColumn(
                name: "discount_kind",
                table: "price_rules");

            migrationBuilder.DropColumn(
                name: "max_qty",
                table: "price_rules");

            migrationBuilder.DropColumn(
                name: "min_qty",
                table: "price_rules");

            migrationBuilder.AddCheckConstraint(
                name: "CK_price_rules_RuleType",
                table: "price_rules",
                sql: "rule_type IN ('Presale','LastMinute','TimeWindow','Dynamic')");
        }
    }
}
