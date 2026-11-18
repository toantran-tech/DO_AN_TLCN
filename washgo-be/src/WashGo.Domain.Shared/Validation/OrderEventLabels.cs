using WashGo.Domain.Shared.Enums;
namespace WashGo.Domain.Shared.Validation;
public static class OrderEventLabels
{
    public static string Title(string type, WashOrderStatus status) => type switch
    {
        "OrderCreated" => "Đã tạo đơn",
        "Deposited" => "Khách đã gửi đồ",
        "MerchantAssigned" => "Đã phân công tiệm giặt",
        "ShipperAssigned" => "Đã phân công Shipper",
        "ShipperReassigned" => "Đã đổi phân công Shipper",
        "ShipperUnassigned" => "Đã hủy phân công Shipper",
        "ReturnReadyConfirmed" => "Tiệm đã chuẩn bị xong, sẵn sàng trả đồ",
        "ShipperCollected" => "Shipper đã lấy đồ về tiệm",
        "ShipperReturned" => "Shipper đã trả đồ vào locker",
        "ActualWeightConfirmed" => "Đã cân và chốt giá",
        "LaundryPaid" => "Đã thanh toán tiền giặt",
        "StorageFeePaid" => "Đã thanh toán phí quá hạn/lưu kho",
        "CancellationFeePaid" => "Đã thanh toán phí hủy",
        "Cancelled" => "Đã hủy đơn",
        "CancelledGoodsReturned" => "Khách đã nhận lại đồ của đơn hủy",
        "Returned" => "Khách đã nhận đồ",
        "Completed" => "Đơn đã hoàn tất",
        "Storage.Assign" => "Đã phân công giao nhận lưu giữ",
        "Storage.RequestReturn" => "Khách yêu cầu giao lại locker",
        "Storage.cancel-return" => "Đã hủy yêu cầu giao lại",
        "Storage.cancel-assignment" => "Đã hủy phân công giao nhận lưu giữ",
        "Storage.collect" => "Đã lấy đồ khỏi locker để chuyển về tiệm",
        "Storage.handover" => "Đã bàn giao lưu giữ tại tiệm",
        "Storage.depart" => "Đồ đã xuất tiệm để giao lại locker",
        "Storage.place" => "Đã giao lại đồ vào locker",
        "Storage.SystemUnpaidAtShop" => "Đồ quá hạn thanh toán đang lưu giữ tại tiệm",
        "Storage.verify-legacy" => "Đã đối chiếu lưu giữ tại tiệm",
        "Storage.exception" => "Sự cố giao nhận lưu giữ",
        "AccessCodesRevealed" => "Admin xem mã hỗ trợ giao nhận",
        "StatusChanged" => status switch
        {
            WashOrderStatus.Washing => "Đang giặt", WashOrderStatus.Drying => "Đang sấy",
            WashOrderStatus.Ironing => "Đang ủi", WashOrderStatus.Folding => "Đang đóng gói",
            WashOrderStatus.AwaitingPayment => "Giặt xong, chờ thanh toán", _ => "Đã cập nhật trạng thái đơn"
        },
        _ => "Đã cập nhật xử lý đơn"
    };
}
