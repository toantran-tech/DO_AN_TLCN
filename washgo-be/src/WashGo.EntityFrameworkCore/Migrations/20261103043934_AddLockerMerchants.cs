using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddLockerMerchants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locker_merchants",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    locker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locker_merchants", x => x.id);
                    table.ForeignKey(
                        name: "FK_locker_merchants_lockers_locker_id",
                        column: x => x.locker_id,
                        principalSchema: "washgo",
                        principalTable: "lockers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_locker_merchants_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "washgo",
                        principalTable: "merchants",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_locker_merchants_locker_id_merchant_id",
                schema: "washgo",
                table: "locker_merchants",
                columns: new[] { "locker_id", "merchant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locker_merchants_merchant_id",
                schema: "washgo",
                table: "locker_merchants",
                column: "merchant_id");

            // Every existing locker keeps its current Merchant as the first (priority 1) Merchant.
            migrationBuilder.Sql(
                "INSERT INTO washgo.locker_merchants (id, locker_id, merchant_id, priority, created_at) " +
                "SELECT gen_random_uuid(), id, merchant_id, 1, now() FROM washgo.lockers WHERE merchant_id IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_lockers_merchants_merchant_id",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropIndex(
                name: "IX_lockers_merchant_id",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropColumn(
                name: "merchant_id",
                schema: "washgo",
                table: "lockers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "merchant_id",
                schema: "washgo",
                table: "lockers",
                type: "uuid",
                nullable: true);

            // Keep the priority-1 Merchant of each locker; lockers without one cannot go back to the old model.
            migrationBuilder.Sql(
                "UPDATE washgo.lockers l SET merchant_id = (SELECT lm.merchant_id FROM washgo.locker_merchants lm " +
                "WHERE lm.locker_id = l.id ORDER BY lm.priority LIMIT 1);");
            migrationBuilder.Sql("DELETE FROM washgo.lockers WHERE merchant_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "merchant_id",
                schema: "washgo",
                table: "lockers",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "locker_merchants",
                schema: "washgo");

            migrationBuilder.CreateIndex(
                name: "IX_lockers_merchant_id",
                schema: "washgo",
                table: "lockers",
                column: "merchant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_lockers_merchants_merchant_id",
                schema: "washgo",
                table: "lockers",
                column: "merchant_id",
                principalSchema: "washgo",
                principalTable: "merchants",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
