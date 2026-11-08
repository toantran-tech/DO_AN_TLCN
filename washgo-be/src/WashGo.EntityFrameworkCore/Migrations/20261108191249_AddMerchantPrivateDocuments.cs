using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantPrivateDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "business_license_document_id",
                schema: "washgo",
                table: "merchants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "contract_document_id",
                schema: "washgo",
                table: "merchants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sla_document_id",
                schema: "washgo",
                table: "merchants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "private_documents",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    extension = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    referenced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_private_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_private_documents_is_deleted_referenced_at_expires_at",
                schema: "washgo",
                table: "private_documents",
                columns: new[] { "is_deleted", "referenced_at", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_private_documents_merchant_id",
                schema: "washgo",
                table: "private_documents",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_private_documents_owner_id",
                schema: "washgo",
                table: "private_documents",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "private_documents",
                schema: "washgo");

            migrationBuilder.DropColumn(
                name: "business_license_document_id",
                schema: "washgo",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "contract_document_id",
                schema: "washgo",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "sla_document_id",
                schema: "washgo",
                table: "merchants");
        }
    }
}
