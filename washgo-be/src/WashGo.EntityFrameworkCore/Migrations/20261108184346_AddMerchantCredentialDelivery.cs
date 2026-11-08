using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantCredentialDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "credential_delivery",
                schema: "washgo",
                table: "merchants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "credential_sent_at",
                schema: "washgo",
                table: "merchants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credential_delivery",
                schema: "washgo",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "credential_sent_at",
                schema: "washgo",
                table: "merchants");
        }
    }
}
