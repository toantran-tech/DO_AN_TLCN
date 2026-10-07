using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Application.Contracts.Features.PaymentContracts.Services;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Application.Contracts.Features.WashOrderContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Entities.Payment;

namespace WashGo.Application.Features.PaymentApplication.Services
{
    public class PaymentAppService : WashGoAppService, IPaymentAppService
    {
        private readonly IWashOrderAppService _orderService;
        private readonly IRepository<PaymentTransaction, Guid> _transactionRepo;

        public PaymentAppService(
            IWashOrderAppService orderService,
            IRepository<PaymentTransaction, Guid> transactionRepo)
        {
            _orderService = orderService;
            _transactionRepo = transactionRepo;
        }

        public async Task<BaseResponse<PaymentResultDto>> CreatePaymentAsync(CreatePaymentCommand command)
        {
            var orderResponse = await _orderService.GetByIdAsync(command.OrderId);
            if (!orderResponse.Succeeded || orderResponse.Data == null)
                return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng cần thanh toán.");

            var order = orderResponse.Data;
            if (order.PaymentStatus == PaymentStatus.Paid)
                return BadRequest<PaymentResultDto>("Đơn hàng đã được thanh toán.");

            if (order.Status != WashOrderStatus.AwaitingPayment)
                return BadRequest<PaymentResultDto>("Đơn hàng chưa ở trạng thái chờ thanh toán.");

            if (command.PaymentMethod == PaymentMethod.Cash)
                return BadRequest<PaymentResultDto>("Payment URL chỉ áp dụng cho phương thức thanh toán trực tuyến.");

            var transactionReference = $"WG-PAY-{order.OrderCode}-{Guid.NewGuid():N}";
            var paymentUrl = $"http://localhost:5000/payment/mock-checkout?orderId={order.Id}&reference={transactionReference}";

            await _transactionRepo.InsertAsync(new PaymentTransaction
            {
                OrderId = order.Id,
                TransactionReference = transactionReference,
                PaymentMethod = command.PaymentMethod,
                Status = PaymentTransactionStatus.Pending,
                Amount = order.FinalAmount,
                PaymentUrl = paymentUrl
            }, autoSave: true);

            return Success(new PaymentResultDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                Amount = order.FinalAmount,
                PaymentMethod = command.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                TransactionReference = transactionReference,
                PaymentUrl = paymentUrl
            }, "Tạo payment URL sandbox thành công.");
        }

        public async Task<BaseResponse<PaymentResultDto>> HandleCallbackAsync(PaymentCallbackCommand command)
        {
            if (!command.Success)
                return BadRequest<PaymentResultDto>("Thanh toán thất bại hoặc bị hủy.");

            if (string.IsNullOrWhiteSpace(command.TransactionReference))
                return BadRequest<PaymentResultDto>("Thiếu mã giao dịch thanh toán.");

            var transaction = await _transactionRepo.FirstOrDefaultAsync(
                x => x.TransactionReference == command.TransactionReference);
            if (transaction == null || transaction.OrderId != command.OrderId)
                return BadRequest<PaymentResultDto>("Mã giao dịch không hợp lệ.");

            var orderResponse = await _orderService.GetByIdAsync(command.OrderId);
            if (!orderResponse.Succeeded || orderResponse.Data == null)
                return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng cần cập nhật thanh toán.");

            var order = orderResponse.Data;
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                if (transaction.Status != PaymentTransactionStatus.Succeeded)
                {
                    transaction.Status = PaymentTransactionStatus.Succeeded;
                    transaction.CompletedAt ??= DateTime.UtcNow;
                    await _transactionRepo.UpdateAsync(transaction, autoSave: true);
                }

                return Success(new PaymentResultDto
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    Amount = order.FinalAmount,
                    PaymentMethod = transaction.PaymentMethod,
                    PaymentStatus = order.PaymentStatus,
                    TransactionReference = command.TransactionReference
                }, "Đơn hàng đã được ghi nhận thanh toán trước đó.");
            }

            var result = await _orderService.UpdateStatusAsync(new UpdateWashOrderStatusCommand
            {
                OrderId = command.OrderId,
                NewStatus = WashOrderStatus.Paid,
                ActorRole = "PaymentGateway",
                ActorName = "WashGo Payment Sandbox",
                Note = $"Thanh toán thành công, mã giao dịch: {command.TransactionReference}"
            });

            if (!result.Succeeded || result.Data == null)
                return BadRequest<PaymentResultDto>(result.Message ?? "Không thể cập nhật trạng thái thanh toán.");

            transaction.Status = PaymentTransactionStatus.Succeeded;
            transaction.CompletedAt = DateTime.UtcNow;
            await _transactionRepo.UpdateAsync(transaction, autoSave: true);

            return Success(new PaymentResultDto
            {
                OrderId = result.Data.Id,
                OrderCode = result.Data.OrderCode,
                Amount = result.Data.FinalAmount,
                PaymentMethod = transaction.PaymentMethod,
                PaymentStatus = result.Data.PaymentStatus,
                TransactionReference = command.TransactionReference
            }, "Thanh toán thành công.");
        }
    }
}
