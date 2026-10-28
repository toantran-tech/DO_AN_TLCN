using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.CustomerContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.CustomerContracts.Services;

public interface ICustomerCatalogAppService : IApplicationService
{
    Task<PaginatedResult<CustomerServiceCatalogDto>> GetServicesAsync(Guid? merchantId, WashServiceType? serviceType, string? keyword, int page, int take);
    Task<BaseResponse<CustomerServiceCatalogDto>> GetServiceAsync(Guid id);
    Task<PaginatedResult<CustomerLockerCatalogDto>> GetLockersAsync(string? city, string? district, string? keyword, int page, int take);
    Task<BaseResponse<CustomerLockerCatalogDto>> GetLockerAsync(Guid id);
}
