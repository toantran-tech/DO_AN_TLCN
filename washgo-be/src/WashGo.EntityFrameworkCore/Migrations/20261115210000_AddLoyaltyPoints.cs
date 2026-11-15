using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace WashGo.EntityFrameworkCore.Migrations;

public partial class AddLoyaltyPoints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "loyalty_version", schema: "washgo", table: "payment_transactions", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<long>(name: "points_to_redeem", schema: "washgo", table: "payment_transactions", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<bool>(name: "points_held", schema: "washgo", table: "payment_transactions", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "loyalty_applied", schema: "washgo", table: "payment_transactions", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<long>(name: "points_earned", schema: "washgo", table: "payment_transactions", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<long>(name: "points_returned", schema: "washgo", table: "payment_transactions", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<long>(name: "reward_points_reversed", schema: "washgo", table: "payment_transactions", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<decimal>(name: "loyalty_earn_percent", schema: "washgo", table: "payment_transactions", type: "numeric(5,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(name: "loyalty_max_use_percent", schema: "washgo", table: "payment_transactions", type: "numeric(5,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(name: "laundry_amount", schema: "washgo", table: "payment_transactions", type: "numeric(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<long>(name: "points_amount", schema: "washgo", table: "refund_requests", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<long>(name: "reward_points_reversed", schema: "washgo", table: "refund_requests", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<decimal>(name: "loyalty_earn_percent", schema: "washgo", table: "system_settings", type: "numeric(5,2)", nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<decimal>(name: "loyalty_max_use_percent", schema: "washgo", table: "system_settings", type: "numeric(5,2)", nullable: false, defaultValue: 20m);
        migrationBuilder.CreateTable(name: "loyalty_wallets", schema: "washgo", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            net_points = table.Column<long>(type: "bigint", nullable: false),
            held_points = table.Column<long>(type: "bigint", nullable: false),
            updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_loyalty_wallets", x => x.id);
            table.CheckConstraint("ck_loyalty_held", "held_points >= 0");
            table.ForeignKey(name: "FK_loyalty_wallets_customers_id", column: x => x.id,
                principalSchema: "washgo", principalTable: "customers", principalColumn: "user_id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable(name: "loyalty_entries", schema: "washgo", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            customer_id = table.Column<Guid>(type: "uuid", nullable: false),
            order_id = table.Column<Guid>(type: "uuid", nullable: true),
            payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
            refund_request_id = table.Column<Guid>(type: "uuid", nullable: true),
            event_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
            kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            points_delta = table.Column<long>(type: "bigint", nullable: false),
            held_delta = table.Column<long>(type: "bigint", nullable: false),
            balance_after = table.Column<long>(type: "bigint", nullable: false),
            held_after = table.Column<long>(type: "bigint", nullable: false),
            created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_loyalty_entries", x => x.id);
            table.ForeignKey(name: "FK_loyalty_entries_loyalty_wallets_customer_id", column: x => x.customer_id,
                principalSchema: "washgo", principalTable: "loyalty_wallets", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_loyalty_entries_event_key", schema: "washgo", table: "loyalty_entries", column: "event_key", unique: true);
        migrationBuilder.CreateIndex(name: "IX_loyalty_entries_customer_id_created_at_id", schema: "washgo", table: "loyalty_entries", columns: new[] { "customer_id", "created_at", "id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM washgo.loyalty_entries)
                   OR EXISTS (SELECT 1 FROM washgo.payment_transactions WHERE loyalty_version <> 0)
                   OR EXISTS (SELECT 1 FROM washgo.refund_requests WHERE points_amount <> 0) THEN
                    RAISE EXCEPTION 'Cannot drop loyalty evidence. Export/review all wallets and payment reservations before rollback.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropTable(name: "loyalty_entries", schema: "washgo");
        migrationBuilder.DropTable(name: "loyalty_wallets", schema: "washgo");
        migrationBuilder.DropColumn(name: "loyalty_version", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "points_to_redeem", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "points_held", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "loyalty_applied", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "points_earned", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "points_returned", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "reward_points_reversed", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "loyalty_earn_percent", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "loyalty_max_use_percent", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "laundry_amount", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "points_amount", schema: "washgo", table: "refund_requests");
        migrationBuilder.DropColumn(name: "reward_points_reversed", schema: "washgo", table: "refund_requests");
        migrationBuilder.DropColumn(name: "loyalty_earn_percent", schema: "washgo", table: "system_settings");
        migrationBuilder.DropColumn(name: "loyalty_max_use_percent", schema: "washgo", table: "system_settings");
    }
}
