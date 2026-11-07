using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperWriteSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "create_request_id",
                schema: "washgo",
                table: "shippers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "credential_delivery",
                schema: "washgo",
                table: "shippers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "credential_sent_at",
                schema: "washgo",
                table: "shippers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                schema: "washgo",
                table: "shippers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_shippers_merchant_id_create_request_id",
                schema: "washgo",
                table: "shippers",
                columns: new[] { "merchant_id", "create_request_id" },
                unique: true,
                filter: "create_request_id IS NOT NULL");

            // Every UPDATE of a shipper row (management, self-profile, delivery counters) bumps its revision.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION washgo.bump_shipper_revision() RETURNS trigger AS $$
BEGIN
    NEW.revision := OLD.revision + 1;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
CREATE TRIGGER trg_shippers_revision BEFORE UPDATE ON washgo.shippers
    FOR EACH ROW EXECUTE FUNCTION washgo.bump_shipper_revision();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_shippers_revision ON washgo.shippers;
DROP FUNCTION IF EXISTS washgo.bump_shipper_revision();");

            migrationBuilder.DropIndex(
                name: "IX_shippers_merchant_id_create_request_id",
                schema: "washgo",
                table: "shippers");

            migrationBuilder.DropColumn(
                name: "create_request_id",
                schema: "washgo",
                table: "shippers");

            migrationBuilder.DropColumn(
                name: "credential_delivery",
                schema: "washgo",
                table: "shippers");

            migrationBuilder.DropColumn(
                name: "credential_sent_at",
                schema: "washgo",
                table: "shippers");

            migrationBuilder.DropColumn(
                name: "revision",
                schema: "washgo",
                table: "shippers");
        }
    }
}
