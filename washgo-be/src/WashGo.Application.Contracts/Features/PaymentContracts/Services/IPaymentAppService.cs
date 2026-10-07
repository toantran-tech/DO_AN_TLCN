using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Services
{
    public interface IPaymentAppService : IApplicationService
    {
        Task<BaseResponse<PaymentResultDto>> CreatePaymentAsync(CreatePaymentCommand command);
        Task<BaseResponse<PaymentResultDto>> HandleCallbackAsync(PaymentCallbackCommand command);
    }
}
