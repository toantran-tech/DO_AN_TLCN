using System;
using System.Threading.Tasks;
using WashGo.Application.Contracts.Features.AccountContracts.Commands;
using WashGo.Application.Contracts.Features.AccountContracts.Queries;
using WashGo.Application.Contracts.Features.AccountContracts.Results;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.AccountContracts.Services;

/// <summary>Trang Admin quản lý tài khoản Customer/Merchant/Shipper/Admin (xem docs/ADMIN_ACCOUNT_MANAGEMENT_API_REVIEW.md).</summary>
public interface IAdminAccountAppService : IApplicationService
{
    Task<BaseResponse<AccountPageDto>> GetListAsync(AccountListQuery query);
    Task<BaseResponse<AccountSummaryDto>> GetSummaryAsync();
    Task<BaseResponse<AccountDetailDto>> GetAsync(Guid id);
    Task<BaseResponse<AccountDetailDto>> CreateAsync(CreateAccountCommand command);
    Task<BaseResponse<AccountDetailDto>> UpdateAsync(Guid id, UpdateAccountCommand command);
    Task<BaseResponse<AccountDetailDto>> SetStatusAsync(Guid id, SetAccountStatusCommand command);
    Task<BaseResponse<AccountDetailDto>> SoftDeleteAsync(Guid id, SoftDeleteAccountCommand command);
    Task<BaseResponse<AccountDetailDto>> RestoreAsync(Guid id, RestoreAccountCommand command);
    Task<BaseResponse<AccountDetailDto>> UpdatePermissionsAsync(Guid id, UpdateAccountPermissionsCommand command);
    Task<BaseResponse<AccountEmailChangeDto>> RequestEmailChangeAsync(Guid id, RequestAccountEmailChangeCommand command);
    Task<BaseResponse<AccountDetailDto>> ConfirmEmailChangeAsync(Guid id, ConfirmAccountEmailChangeCommand command);
    Task<BaseResponse<AccountBulkPreviewDto>> PreviewBulkAsync(AccountBulkPreviewCommand command);
    Task<BaseResponse<AdministrativeBatchResultDto>> BulkAsync(AccountBulkCommand command);
    Task<PaginatedResult<AuditLogDto>> GetActivitiesAsync(Guid id, AccountActivityQuery query);
    /// <summary>Kết quả thao tác của chính Admin trong module Account (kể cả tạo Merchant/Shipper đã commit nhưng mất phản hồi).</summary>
    Task<BaseResponse<AdministrativeOperationDto>> GetOperationAsync(Guid operationId);
}
