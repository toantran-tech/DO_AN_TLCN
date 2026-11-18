using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <summary>
    /// Đơn cũ đang ở giai đoạn tiếp nhận/trả đồ chuyển sang trạng thái phân công mới (16 MerchantAssigned,
    /// 17 CollectionShipperAssigned, 19 ReturnShipperAssigned) theo dữ liệu phân công thật trên đơn. Không sửa timeline,
    /// không tạo task/sự kiện; Paid chưa có chuyến trả vẫn là Paid (Merchant xác nhận sẵn sàng theo luồng mới).
    /// Revision tăng qua trigger cập nhật của bảng orders.
    /// </summary>
    public partial class OrderAssignmentAndReturnStatuses : Migration
    {
        // Order-level Collect/Return assignment of a Shipper of the same shop, not contradicted by an open delivery task.
        private const string ValidAssignment = """
            o.shipper_id IS NOT NULL
            AND EXISTS (SELECT 1 FROM washgo.shippers s WHERE s.id = o.shipper_id AND s.merchant_id = o.merchant_id)
            AND NOT EXISTS (SELECT 1 FROM washgo.delivery_tasks t WHERE t.order_id = o.id AND t.status = 1
                            AND (t.shipper_id <> o.shipper_id OR t.task_type <> o.assigned_task_type))
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE washgo.orders o SET status = 17
                WHERE o.is_deleted = false AND o.status = 2 AND o.merchant_id IS NOT NULL AND o.assigned_task_type = 1
                  AND {ValidAssignment};
                UPDATE washgo.orders o SET status = 16
                WHERE o.is_deleted = false AND o.status = 2 AND o.merchant_id IS NOT NULL;
                UPDATE washgo.orders o SET status = 19
                WHERE o.is_deleted = false AND o.status = 9 AND o.payment_status = 2 AND o.merchant_id IS NOT NULL
                  AND o.assigned_task_type = 2 AND {ValidAssignment};
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE washgo.orders SET status = 2 WHERE status IN (16, 17);
                UPDATE washgo.orders SET status = 9 WHERE status IN (18, 19);
                """);
        }
    }
}
