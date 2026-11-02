using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Commands
{
    /// <summary>Báo giá đơn trước khi tạo: tính lại giá từ server và kiểm tra mã giảm giá (nếu có).</summary>
    public class ApplyVoucherCommand
    {
        public Guid LockerId { get; set; }
        public List<CreateWashOrderItemDto> Items { get; set; } = [];
        public string? PromotionCode { get; set; }
    }
}
