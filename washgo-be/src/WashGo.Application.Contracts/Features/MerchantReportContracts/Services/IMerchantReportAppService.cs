using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.MerchantReportContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantReportContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.MerchantReportContracts.Services
{
    public interface IMerchantReportAppService : IApplicationService
    {
        Task<BaseResponse<MerchantReportDto>> GetOverviewAsync(MerchantReportQuery query);
        Task<BaseResponse<ReportFileDto>> ExportCsvAsync(MerchantReportQuery query);
    }
}
