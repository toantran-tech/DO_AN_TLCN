using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace WashGo.EntityFrameworkCore.Migrations;

public partial class AddPaymentVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "gateway_created_at", "gateway_expires_at", "verified_closed_at" })
            migrationBuilder.AddColumn<DateTime>(column, schema: "washgo", table: "payment_transactions",
                type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateTable("payment_verifications", schema: "washgo", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            customer_id = table.Column<Guid>(type: "uuid", nullable: false),
            order_id = table.Column<Guid>(type: "uuid", nullable: false),
            purpose = table.Column<int>(type: "integer", nullable: false),
            transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
            parent_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
            state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
            is_active = table.Column<bool>(type: "boolean", nullable: false),
            attempts = table.Column<int>(type: "integer", nullable: false),
            created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            lease_token = table.Column<Guid>(type: "uuid", nullable: true),
            reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
        }, constraints: table => table.PrimaryKey("PK_payment_verifications", x => x.id));
        migrationBuilder.CreateIndex("IX_payment_verifications_is_active_next_attempt_at", "payment_verifications",
            new[] { "is_active", "next_attempt_at" }, schema: "washgo");
        migrationBuilder.CreateIndex("IX_payment_verifications_transaction_id_created_at", "payment_verifications",
            new[] { "transaction_id", "created_at" }, schema: "washgo");
        migrationBuilder.CreateIndex("ux_payment_verification_active", "payment_verifications", "transaction_id",
            schema: "washgo", unique: true, filter: "is_active = true AND transaction_id IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM washgo.payment_verifications)
                 OR EXISTS (SELECT 1 FROM washgo.payment_transactions WHERE verified_closed_at IS NOT NULL) THEN
                RAISE EXCEPTION 'Payment verification evidence exists. Archive/reconcile explicitly before rollback.';
              END IF;
            END $$;
            """);
        migrationBuilder.DropTable("payment_verifications", "washgo");
        foreach (var column in new[] { "gateway_created_at", "gateway_expires_at", "verified_closed_at" })
            migrationBuilder.DropColumn(column, "payment_transactions", "washgo");
    }
}
