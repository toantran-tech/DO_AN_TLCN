using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperAccountLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lock_reason",
                schema: "washgo",
                table: "shippers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "locked_at",
                schema: "washgo",
                table: "shippers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "shipper_audits",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipper_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipper_audits", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipper_audits_merchant_id",
                schema: "washgo",
                table: "shipper_audits",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipper_audits_shipper_id_created_at",
                schema: "washgo",
                table: "shipper_audits",
                columns: new[] { "shipper_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipper_audits",
                schema: "washgo");

            migrationBuilder.DropColumn(
                name: "lock_reason",
                schema: "washgo",
                table: "shippers");

            migrationBuilder.DropColumn(
                name: "locked_at",
                schema: "washgo",
                table: "shippers");
        }
    }
}
