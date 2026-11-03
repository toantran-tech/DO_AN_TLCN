namespace WashGo.Application.Features.OrderAssignmentApplication.Services
{
    /// <summary>Quy tắc dùng chung cho phân công đơn cho Merchant (UC 3.3).</summary>
    public static class OrderAssignmentRules
    {
        /// <summary>Đơn đã gửi đồ chờ phân công quá số phút này thì được highlight và nhắc Admin.</summary>
        public const int AssignmentOverdueMinutes = 60;

        public const string NoMerchantInArea = "Không có Merchant nào phục vụ khu vực này.";
        public const string NoSuitableMerchant = "Không tìm thấy Merchant phù hợp cho đơn này.";
    }
}
