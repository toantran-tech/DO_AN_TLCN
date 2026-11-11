using System;

namespace WashGo.Application.Contracts.Features.FinanceContracts.Commands
{
    /// <summary>
    /// Admin xác nhận/sửa tỷ lệ hoa hồng của một đơn dựa trên bằng chứng (hợp đồng, biên bản…), có ghi nhật ký.
    /// Không áp dụng cho đơn đã có khoản thu/hoàn nằm trong kỳ đối soát đã chốt.
    /// </summary>
    public class ReconcileCommissionCommand
    {
        /// <summary>Tỷ lệ % (0-100, tối đa 2 chữ số thập phân); 0 là hợp lệ.</summary>
        public decimal? Rate { get; set; }
        /// <summary>Bằng chứng/căn cứ (10-1000 ký tự), bắt buộc.</summary>
        public string? Evidence { get; set; }
        /// <summary>Tỷ lệ đang áp dụng lúc Admin xem (null nếu đang thiếu); lệch → 409.</summary>
        public decimal? ExpectedRate { get; set; }
        public bool ExpectedMissing { get; set; }
        public Guid? OperationId { get; set; }
    }
}
