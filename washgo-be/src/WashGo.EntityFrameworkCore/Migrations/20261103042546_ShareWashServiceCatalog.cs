using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class ShareWashServiceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_services_merchants_merchant_id",
                schema: "washgo",
                table: "services");

            migrationBuilder.AlterColumn<Guid>(
                name: "merchant_id",
                schema: "washgo",
                table: "services",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_services_merchants_merchant_id",
                schema: "washgo",
                table: "services",
                column: "merchant_id",
                principalSchema: "washgo",
                principalTable: "merchants",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_services_merchants_merchant_id",
                schema: "washgo",
                table: "services");

            migrationBuilder.AlterColumn<Guid>(
                name: "merchant_id",
                schema: "washgo",
                table: "services",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_services_merchants_merchant_id",
                schema: "washgo",
                table: "services",
                column: "merchant_id",
                principalSchema: "washgo",
                principalTable: "merchants",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
