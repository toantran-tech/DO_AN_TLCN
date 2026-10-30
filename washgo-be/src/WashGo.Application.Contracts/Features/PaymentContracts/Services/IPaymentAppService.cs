using System.Threading.Tasks;
using System.Collections.Generic;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Services
{
    public interface IPaymentAppService : IApplicationService
    {
        Task<BaseResponse<PaymentResultDto>> CreatePaymentAsync(CreatePaymentCommand command);
        Task<BaseResponse<PaymentResultDto>> CreateCancellationFeePaymentAsync(CreatePaymentCommand command);
        Task<BaseResponse<PaymentResultDto>> CreateOverdueFeePaymentAsync(CreateOverdueFeePaymentCommand command);
        Task<BaseResponse<PaymentResultDto>> HandleCallbackAsync(PaymentCallbackCommand command);
        Task<VnPayIpnResultDto> HandleVnPayIpnAsync(IReadOnlyDictionary<string, string> parameters);
        Task<BaseResponse<RefundRequestDto>> CreateRefundRequestAsync(CreateRefundRequestCommand command);
        Task<BaseResponse<System.Collections.Generic.List<RefundRequestDto>>> GetRefundRequestsAsync(bool mineOnly);
        Task<BaseResponse<RefundRequestDto>> ReviewRefundRequestAsync(Guid id, ReviewRefundRequestCommand command);
        Task<BaseResponse<RefundRequestDto>> ConfirmRefundedAsync(Guid id, ConfirmRefundCommand command);
    }
}
