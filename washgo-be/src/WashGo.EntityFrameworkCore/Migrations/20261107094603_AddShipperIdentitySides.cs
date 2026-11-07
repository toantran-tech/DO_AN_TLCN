using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperIdentitySides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identity_back_image_url",
                schema: "washgo",
                table: "shippers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_front_image_url",
                schema: "washgo",
                table: "shippers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "identity_back_image_url",
                schema: "washgo",
                table: "shippers");

            migrationBuilder.DropColumn(
                name: "identity_front_image_url",
                schema: "washgo",
                table: "shippers");
        }
    }
}
