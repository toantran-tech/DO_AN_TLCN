using System.Text.RegularExpressions;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.CustomerContracts;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;

namespace WashGo.Application.Features.CustomerApplication.Services;

public class CustomerAppService : WashGoAppService, ICustomerAppService
{
    private readonly IRepository<User, Guid> _users;
    private readonly IRepository<Customer, Guid> _customers;

    public CustomerAppService(IRepository<User, Guid> users, IRepository<Customer, Guid> customers)
    {
        _users = users;
        _customers = customers;
    }

    public async Task<BaseResponse<CustomerProfileDto>> GetMyProfileAsync()
    {
        var id = GetCustomerId();
        var user = await _users.FindAsync(id);
        var customer = await _customers.FindAsync(id);
        if (user == null || !user.IsActive || customer == null)
            return NotFound<CustomerProfileDto>("Không tìm thấy hồ sơ khách hàng đang hoạt động.");

        return Success(ToDto(user, customer));
    }

    public async Task<BaseResponse<CustomerProfileDto>> UpdateMyProfileAsync(UpdateCustomerProfileCommand command)
    {
        var id = GetCustomerId();
        var name = command.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length is < 2 or > 100 || name.Any(char.IsControl))
            return BadRequest<CustomerProfileDto>("Họ tên phải có từ 2 đến 100 ký tự và không chứa ký tự điều khiển.");

        // Accept common display separators, but store only ASCII digits, matching registration.
        var phone = Clean(command.Phone);
        if (phone != null)
        {
            if (phone.Length > 30 || !Regex.IsMatch(phone, @"^\+?[0-9 ()\-]+$"))
                return BadRequest<CustomerProfileDto>("Số điện thoại không đúng định dạng.");
            phone = new string(phone.Where(c => c is >= '0' and <= '9').ToArray());
            if (phone.Length is < 9 or > 15)
                return BadRequest<CustomerProfileDto>("Số điện thoại phải có từ 9 đến 15 chữ số.");
        }

        var address = Clean(command.Address);
        var district = Clean(command.District);
        var city = Clean(command.City);
        if (address?.Length > 500 || district?.Length > 100 || city?.Length > 100)
            return BadRequest<CustomerProfileDto>("Địa chỉ tối đa 500 ký tự; quận/huyện và tỉnh/thành phố tối đa 100 ký tự.");

        // Both tables must commit together, even when this service is called outside HTTP.
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var user = await _users.FindAsync(id);
        var customer = await _customers.FindAsync(id);
        if (user == null || !user.IsActive || customer == null)
            return NotFound<CustomerProfileDto>("Không tìm thấy hồ sơ khách hàng đang hoạt động.");

        user.FullName = name;
        user.Phone = phone;
        user.UpdatedAt = DateTime.UtcNow;
        customer.Address = address;
        customer.District = district;
        customer.City = city;
        customer.UpdatedAt = user.UpdatedAt;
        await _users.UpdateAsync(user);
        await _customers.UpdateAsync(customer);
        await uow.CompleteAsync();
        return Success(ToDto(user, customer), "Cập nhật hồ sơ khách hàng thành công.");
    }

    private Guid GetCustomerId()
    {
        if (!CurrentUser.IsInRole("Customer") || !CurrentUser.Id.HasValue)
            throw new AbpAuthorizationException();
        return CurrentUser.Id.Value;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CustomerProfileDto ToDto(User user, Customer customer) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        Phone = user.Phone,
        Address = customer.Address,
        District = customer.District,
        City = customer.City,
        LoyaltyPoints = customer.LoyaltyPoints,
        TotalOrders = customer.TotalOrders,
        TotalSpent = customer.TotalSpent
    };
}
