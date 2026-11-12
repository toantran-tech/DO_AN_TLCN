using System;

namespace WashGo.Domain.Entities.Finance
{
    /// <summary>
    /// Quy tắc hoa hồng dùng chung cho báo cáo Merchant, đối soát, chi tiết đơn và tổng lũy kế:
    /// tính theo TỪNG giao dịch (mỗi khoản thu, mỗi khoản hoàn) với tỷ lệ chốt của chính giao dịch đó, làm tròn đến đồng,
    /// nửa đồng làm tròn ra xa số 0 (giống round(numeric) của PostgreSQL dùng trong truy vấn tổng hợp).
    /// Hoa hồng = Σ hoa hồng các khoản thu − Σ hoa hồng các khoản hoàn.
    /// </summary>
    public static class CommissionMath
    {
        public const string Rule = "PerTransaction:RoundHalfAwayFromZero:VND";

        public static decimal Of(decimal amount, decimal ratePercent) =>
            decimal.Round(amount * ratePercent / 100m, 0, MidpointRounding.AwayFromZero);
    }
}
