using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results
{
    /// <summary>
    /// Biên lai thanh toán nội bộ của WashGo cho một đơn (không phải hóa đơn GTGT điện tử theo luật thuế).
    /// Số biên lai và ngày xuất cố định từ lần xuất đầu tiên.
    /// </summary>
    public class OrderReceiptDto
    {
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string? MerchantOrderCode { get; set; }
        public WashOrderStatus OrderStatus { get; set; }
        public ReceiptPartyDto Seller { get; set; } = new();
        public ReceiptPartyDto Buyer { get; set; } = new();
        public List<ReceiptLineDto> Lines { get; set; } = [];
        public decimal ServicesAmount { get; set; }
        public decimal WeightSurchargeAmount { get; set; }
        public string? PromotionCode { get; set; }
        public decimal DiscountAmount { get; set; }
        /// <summary>Tiền giặt sau giảm giá (đã gồm phụ phí vượt cân).</summary>
        public decimal LaundryAmount { get; set; }
        /// <summary>Các khoản phí đã thu thêm: phí lưu kho/quá hạn, phí hủy.</summary>
        public List<ReceiptLineDto> Fees { get; set; } = [];
        public decimal TotalPaid { get; set; }
        public string Currency { get; set; } = "VND";
        /// <summary>Đồ án chưa đăng ký thuế suất riêng: null nghĩa là giá đã gồm thuế (nếu có).</summary>
        public decimal? VatRate { get; set; }
        public string Note { get; set; } = string.Empty;
        public List<ReceiptPaymentDto> Payments { get; set; } = [];
    }

    public class ReceiptPartyDto
    {
        public string Name { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    public class ReceiptLineDto
    {
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }

    public class ReceiptPaymentDto
    {
        public PaymentMethod Method { get; set; }
        public PaymentPurpose Purpose { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string? ProviderTransactionId { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class ReceiptFileDto
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = [];
    }
}
