using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPaymentDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UC 1.2.4 7b: unpaid orders already waiting for payment get the 7-day deadline counted from the
            // moment they became "Chờ thanh toán" (or from now when that history row is missing).
            migrationBuilder.Sql(@"
UPDATE washgo.orders o
SET payment_deadline = COALESCE(
        (SELECT to_timestamp(max(h.created_at)) FROM washgo.order_status_history h WHERE h.order_id = o.id AND h.to_status = 8),
        now()) + interval '7 days'
WHERE o.status = 8 AND o.payment_status = 1 AND o.payment_deadline IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
