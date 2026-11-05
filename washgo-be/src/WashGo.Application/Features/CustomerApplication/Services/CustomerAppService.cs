using Microsoft.Extensions.Configuration;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.CustomerContracts.Commands;
using WashGo.Application.Contracts.Features.CustomerContracts.Results;
using WashGo.Application.Contracts.Features.CustomerContracts.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Shared.Validation;

namespace WashGo.Application.Features.CustomerApplication.Services;

public class CustomerAppService : WashGoAppService, ICustomerAppService
{
    private readonly IRepository<User, Guid> _users;
    private readonly IRepository<Customer, Guid> _customers;
    private readonly IWashOrderRepository _orders;
    private readonly IConfiguration _configuration;

    public CustomerAppService(IRepository<User, Guid> users, IRepository<Customer, Guid> customers,
        IWashOrderRepository orders, IConfiguration configuration)
    {
        _users = users;
        _customers = customers;
        _orders = orders;
        _configuration = configuration;
    }

    public async Task<BaseResponse<CustomerProfileDto>> GetMyProfileAsync()
    {
        var id = GetCustomerId();
        var user = await _users.FindAsync(id);
        var customer = await _customers.FindAsync(id);
        if (user == null || !user.IsActive || customer == null)
            return NotFound<CustomerProfileDto>("Không tìm thấy hồ sơ khách hàng đang hoạt động.");

        return Success(await ToDtoAsync(user, customer));
    }

    public async Task<BaseResponse<CustomerProfileDto>> UpdateMyProfileAsync(UpdateCustomerProfileCommand command)
    {
        var id = GetCustomerId();
        var name = command.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length is < 2 or > 100 || name.Any(char.IsControl))
            return BadRequest<CustomerProfileDto>("Họ tên phải có từ 2 đến 100 ký tự và không chứa ký tự điều khiển.");

        // Same ten-digit local phone format as registration.
        var phone = Clean(command.Phone);
        if (!CustomerPhoneRules.IsValid(phone))
            return BadRequest<CustomerProfileDto>("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");
        var address = Clean(command.Address);
        // UC 1.1.5 9b: the address is required.
        if (address == null)
            return BadRequest<CustomerProfileDto>("Vui lòng nhập địa chỉ.");
        var city = Clean(command.City);
        if (city == null)
            return BadRequest<CustomerProfileDto>("Vui lòng nhập tỉnh/thành phố.");
        if (address.Length > 500 || city.Length > 100)
            return BadRequest<CustomerProfileDto>("Địa chỉ tối đa 500 ký tự; tỉnh/thành phố tối đa 100 ký tự.");
        // Both tables must commit together, even when this service is called outside HTTP.
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var user = await _users.FindAsync(id);
        var customer = await _customers.FindAsync(id);
        if (user == null || !user.IsActive || customer == null)
            return NotFound<CustomerProfileDto>("Không tìm thấy hồ sơ khách hàng đang hoạt động.");

        if (command.AvatarUrl != null)
        {
            var avatar = Clean(command.AvatarUrl);
            // A new avatar must be an existing upload of this account (UC 1.1.5 9c).
            if (avatar != null && avatar != user.Avatar &&
                UploadStorage.ValidateOwnedImages(_configuration, [avatar], user.Id, 1, out _) != null)
                return BadRequest<CustomerProfileDto>("Không thể upload ảnh. Vui lòng thử lại.");
            user.Avatar = avatar;
        }
        user.FullName = name;
        user.Phone = phone;
        user.UpdatedAt = DateTime.UtcNow;
        customer.Address = address;
        customer.City = city;
        customer.UpdatedAt = user.UpdatedAt;
        await _users.UpdateAsync(user);
        await _customers.UpdateAsync(customer);
        await uow.CompleteAsync();
        return Success(await ToDtoAsync(user, customer), "Cập nhật hồ sơ thành công!");
    }

    private Guid GetCustomerId()
    {
        if (!CurrentUser.IsInRole("Customer") || !CurrentUser.Id.HasValue)
            throw new AbpAuthorizationException();
        return CurrentUser.Id.Value;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // The stored order counters are never incremented, so totals come from the orders themselves (UC 1.1.4).
    private async Task<CustomerProfileDto> ToDtoAsync(User user, Customer customer)
    {
        var orders = (await _orders.GetQueryableAsync()).Where(x => x.CustomerId == user.Id && !x.IsDeleted);
        var dto = ToDto(user, customer);
        dto.TotalOrders = await AsyncExecuter.CountAsync(orders.Where(x => x.Status != WashOrderStatus.Cancelled));
        dto.TotalSpent = await AsyncExecuter.SumAsync(orders.Where(x => x.PaymentStatus == PaymentStatus.Paid), x => x.FinalAmount);
        return dto;
    }

    private static CustomerProfileDto ToDto(User user, Customer customer) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        Phone = user.Phone,
        AvatarUrl = user.Avatar,
        Address = customer.Address,
        City = customer.City,
        LoyaltyPoints = customer.LoyaltyPoints,
        TotalOrders = customer.TotalOrders,
        TotalSpent = customer.TotalSpent
    };
}
