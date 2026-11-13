using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Application.Contracts.Features.WashOrderContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Payment.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.WashOrderApplication.Receipts
{
    /// <summary>
    /// Biên lai thanh toán của một đơn (mục 9 yêu cầu Customer): xuất khi đơn đã có khoản thanh toán thành công,
    /// cho khách chủ đơn, Merchant của đơn và Admin. Đây là biên lai nội bộ, không phải hóa đơn GTGT điện tử.
    /// </summary>
    public class OrderReceiptAppService : WashGoAppService, IOrderReceiptAppService
    {
        public const string ReceiptNote =
            "Biên lai thanh toán nội bộ của nền tảng WashGo (thu hộ qua cổng thanh toán). Giá đã bao gồm các loại thuế (nếu có). " +
            "Đây không phải hóa đơn giá trị gia tăng điện tử.";

        private readonly IWashOrderRepository _orderRepo;
        private readonly IPaymentTransactionRepository _transactionRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<User, Guid> _userRepo;

        public OrderReceiptAppService(IWashOrderRepository orderRepo, IPaymentTransactionRepository transactionRepo,
            IRepository<Merchant, Guid> merchantRepo, IRepository<User, Guid> userRepo)
        {
            _orderRepo = orderRepo;
            _transactionRepo = transactionRepo;
            _merchantRepo = merchantRepo;
            _userRepo = userRepo;
        }

        public async Task<BaseResponse<OrderReceiptDto>> GetAsync(Guid orderId)
        {
            var (receipt, error, notFound) = await BuildAsync(orderId);
            if (receipt != null) return Success(receipt);
            return notFound ? NotFound<OrderReceiptDto>(error!) : BadRequest<OrderReceiptDto>(error!);
        }

        public async Task<BaseResponse<ReceiptFileDto>> GetPdfAsync(Guid orderId)
        {
            var (receipt, error, notFound) = await BuildAsync(orderId);
            if (receipt == null)
                return notFound ? NotFound<ReceiptFileDto>(error!) : BadRequest<ReceiptFileDto>(error!);
            return Success(new ReceiptFileDto
            {
                FileName = $"bien-lai-{receipt.ReceiptNumber}.pdf",
                Content = ReceiptPdfRenderer.Render(receipt)
            });
        }

        private async Task<(OrderReceiptDto? Receipt, string? Error, bool NotFound)> BuildAsync(Guid orderId)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            var userId = CurrentUser.Id;
            var allowed = order != null && (CurrentUser.IsInRole("Admin") ||
                CurrentUser.IsInRole("Customer") && order.CustomerId == userId ||
                CurrentUser.IsInRole("Merchant") && order.MerchantId == userId);
            if (order == null || !allowed)
                return (null, "Không tìm thấy đơn hàng.", true);

            // Refunded transactions are no longer money kept, so only succeeded ones appear on the receipt.
            var payments = (await _transactionRepo.GetListAsync(x => x.OrderId == orderId && x.Status == PaymentTransactionStatus.Succeeded))
                .OrderBy(x => x.CompletedAt).ToList();
            if (payments.Count == 0)
                return (null, "Đơn chưa thanh toán nên chưa thể xuất biên lai.", false);

            // The number and date are fixed the first time the receipt is issued.
            if (order.ReceiptNumber == null)
            {
                order.ReceiptNumber = "BL" + order.OrderCode[2..];
                // Whole seconds, so the first response equals every later read from the database.
                var now = DateTime.UtcNow;
                order.ReceiptIssuedAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
                await _orderRepo.UpdateAsync(order, autoSave: true);
            }

            var merchant = order.MerchantId.HasValue ? await _merchantRepo.FindAsync(order.MerchantId.Value) : null;
            var customer = await _userRepo.FindAsync(order.CustomerId);
            var laundryPaid = payments.Where(x => x.Purpose == PaymentPurpose.Order).Sum(x => x.Amount);
            var storagePaid = payments.Where(x => x.Purpose == PaymentPurpose.OverdueStorageFee).Sum(x => x.Amount);
            var cancelPaid = payments.Where(x => x.Purpose == PaymentPurpose.CancellationFee).Sum(x => x.Amount);
            var receipt = new OrderReceiptDto
            {
                ReceiptNumber = order.ReceiptNumber,
                IssuedAt = order.ReceiptIssuedAt!.Value,
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                MerchantOrderCode = order.MerchantOrderCode,
                OrderStatus = order.Status,
                Seller = new ReceiptPartyDto
                {
                    Name = merchant?.BusinessName ?? order.MerchantName ?? "WashGo",
                    TaxCode = merchant?.TaxCode,
                    Address = merchant == null ? null
                        : string.Join(", ", new[] { merchant.Address, merchant.District, merchant.City }.Where(x => !string.IsNullOrWhiteSpace(x))),
                    Phone = merchant?.Phone
                },
                Buyer = new ReceiptPartyDto
                {
                    Name = order.CustomerName,
                    Phone = order.CustomerPhoneNumber,
                    Email = customer?.Email
                },
                Lines = order.Items.Select(x => new ReceiptLineDto
                {
                    Description = x.ServiceName,
                    Quantity = x.Quantity,
                    WeightKg = x.IsWeightBased ? x.WeightKg : null,
                    UnitPrice = x.UnitPrice,
                    Amount = x.SubTotal
                }).ToList(),
                ServicesAmount = order.Items.Sum(x => x.SubTotal),
                WeightSurchargeAmount = order.WeightSurchargeAmount,
                PromotionCode = order.PromotionCode,
                DiscountAmount = order.DiscountAmount,
                LaundryAmount = laundryPaid,
                Note = ReceiptNote,
                Payments = payments.Select(x => new ReceiptPaymentDto
                {
                    Method = x.PaymentMethod,
                    Purpose = x.Purpose,
                    Amount = x.Amount,
                    Reference = x.TransactionReference,
                    ProviderTransactionId = x.ProviderTransactionId,
                    PaidAt = x.CompletedAt
                }).ToList()
            };
            if (storagePaid > 0)
                receipt.Fees.Add(new ReceiptLineDto { Description = "Phí quá hạn / lưu kho", Quantity = 1, UnitPrice = storagePaid, Amount = storagePaid });
            if (cancelPaid > 0)
                receipt.Fees.Add(new ReceiptLineDto { Description = "Phí hủy đơn", Quantity = 1, UnitPrice = cancelPaid, Amount = cancelPaid });
            receipt.TotalPaid = laundryPaid + storagePaid + cancelPaid;
            if (CurrentUser.IsInRole("Customer"))
            {
                receipt.MerchantOrderCode = null;
                receipt.Seller = new ReceiptPartyDto { Name = "WashGo" };
            }
            else if (!CurrentUser.IsInRole("Admin"))
                receipt.Buyer = new ReceiptPartyDto { Name = "Khách hàng" };
            await uow.CompleteAsync();
            return (receipt, null, false);
        }
    }
}
