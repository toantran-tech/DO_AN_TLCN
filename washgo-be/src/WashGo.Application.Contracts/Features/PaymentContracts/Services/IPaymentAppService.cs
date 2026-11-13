using System;
using WashGo.Domain.Shared.Enums;
using System.Threading.Tasks;
using System.Collections.Generic;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Application.Contracts.Features.PaymentContracts.Queries;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Services
{
    public interface IPaymentAppService : IApplicationService
    {
        Task<BaseResponse<PaymentResultDto>> CreatePaymentAsync(CreatePaymentCommand command);
        Task<BaseResponse<PaymentResultDto>> CreateCancellationFeePaymentAsync(CreatePaymentCommand command);
        Task<BaseResponse<PaymentResultDto>> CreateOverdueFeePaymentAsync(CreateOverdueFeePaymentCommand command);
        Task<BaseResponse<PaymentResultDto>> HandleCallbackAsync(PaymentCallbackCommand command);
        Task<VnPayIpnResultDto> HandleVnPayIpnAsync(IReadOnlyDictionary<string, string> parameters);
        /// <summary>False when the IPN signature/partner is invalid (the endpoint answers 400).</summary>
        Task<bool> HandleMomoIpnAsync(MomoIpnCommand ipn);
        Task<BaseResponse<RefundRequestDto>> CreateRefundRequestAsync(CreateRefundRequestCommand command);
        Task<BaseResponse<List<RefundRequestDto>>> GetRefundRequestsAsync(bool mineOnly, RefundRequestStatus? status = null);
        Task<BaseResponse<RefundRequestDto>> GetRefundRequestAsync(Guid id);
        Task<PaginatedResult<RefundRequestDto>> FilterRefundRequestsAsync(RefundFilterQuery query);
        Task<BaseResponse<RefundRequestDto>> ReviewRefundRequestAsync(Guid id, ReviewRefundRequestCommand command);
        Task<BaseResponse<RefundRequestDto>> ConfirmRefundedAsync(Guid id, ConfirmRefundCommand command);
    }
}
