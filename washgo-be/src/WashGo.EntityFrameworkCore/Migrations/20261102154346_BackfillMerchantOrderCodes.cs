using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class BackfillMerchantOrderCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orders created before UC 2.3.1 had no Merchant order code; derive it from the unique order code.
            migrationBuilder.Sql(
                "UPDATE washgo.orders SET merchant_order_code = 'MC' || substring(order_code from 3) WHERE merchant_order_code IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE washgo.orders SET merchant_order_code = NULL WHERE merchant_order_code LIKE 'MC%';");
        }
    }
}
