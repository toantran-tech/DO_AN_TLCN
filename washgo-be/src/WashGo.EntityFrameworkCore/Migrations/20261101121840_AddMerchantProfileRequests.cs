using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantProfileRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "merchant_profile_requests",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_data = table.Column<string>(type: "jsonb", nullable: false),
                    current_data = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_profile_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_merchant_profile_requests_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "washgo",
                        principalTable: "merchants",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_merchant_profile_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_profile_requests_merchant_id_status",
                schema: "washgo",
                table: "merchant_profile_requests",
                columns: new[] { "merchant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_profile_requests_reviewed_by",
                schema: "washgo",
                table: "merchant_profile_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_profile_requests_status",
                schema: "washgo",
                table: "merchant_profile_requests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "merchant_profile_requests",
                schema: "washgo");
        }
    }
}
