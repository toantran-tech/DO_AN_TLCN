using System;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Features.MerchantApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Services.Identity;

namespace WashGo.Application.Features.AccountApplication.Services;

/// <summary>
/// Một quy tắc định danh cho mọi luồng tạo/đổi email (đăng ký, Admin tạo tài khoản, Merchant, Shipper, đổi email):
/// khóa tư vấn theo email (cùng khóa với tạo Merchant) trong transaction, kiểm tra trên tài khoản chưa xóa,
/// unique index có điều kiện ở DB là chốt chặn cuối và được trả thành 409 đúng trường.
/// </summary>
public class AccountIdentityGuard(IWashOrderRepository locks, IIdentityConflictClassifier conflicts, IRepository<User, Guid> users)
    : ITransientDependency
{
    public const string EmailInUseCode = "EmailAlreadyInUse";
    public const string EmailInUseMessage = "Email này đã được tài khoản khác sử dụng.";

    /// <summary>Call inside a transactional UoW, before checking and writing the email.</summary>
    public Task LockEmailAsync(string normalizedEmail) =>
        locks.AcquireLockAsync($"merchant-email:{MerchantWriteGuard.Hash(normalizedEmail)}");

    public Task<bool> EmailInUseAsync(string normalizedEmail, Guid? exceptUserId = null) =>
        users.AnyAsync(x => x.Email.ToLower() == normalizedEmail && !x.IsDeleted && x.Id != exceptUserId);

    /// <summary>EmailAlreadyUsed / IdentityAlreadyInUse for a unique violation on identity columns; null for anything else.</summary>
    public string? Classify(Exception exception) => conflicts.Classify(exception);

    public static BaseResponse<T> EmailConflict<T>(T? data = default) => new()
    {
        Succeeded = false, StatusCode = HttpStatusCode.Conflict, Data = data, Message = EmailInUseMessage,
        Meta = new { code = EmailInUseCode, fields = new[] { "email" } },
        Errors = new() { ["email"] = [EmailInUseMessage] }
    };
}
