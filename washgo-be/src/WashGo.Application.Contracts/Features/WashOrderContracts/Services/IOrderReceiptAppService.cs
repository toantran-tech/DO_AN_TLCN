using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Services
{
    public interface IOrderReceiptAppService : IApplicationService
    {
        Task<BaseResponse<OrderReceiptDto>> GetAsync(Guid orderId);
        Task<BaseResponse<ReceiptFileDto>> GetPdfAsync(Guid orderId);
    }
}
