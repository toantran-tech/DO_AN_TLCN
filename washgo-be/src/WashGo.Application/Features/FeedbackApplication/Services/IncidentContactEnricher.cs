using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using WashGo.Application.Contracts.Features.FeedbackContracts.Results;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.FeedbackApplication.Services;

public class IncidentContactEnricher(IRepository<User, Guid> users, IRepository<Merchant, Guid> merchants,
    IRepository<MerchantImage, Guid> images, ICurrentUser currentUser) : ITransientDependency
{
    public async Task FillAsync(List<IncidentResultDto> rows)
    {
        if (!currentUser.IsInRole("Admin") || rows.Count == 0) return;
        var ids = rows.Select(x => x.CustomerId).Concat(rows.Where(x => x.MerchantId.HasValue).Select(x => x.MerchantId!.Value)).Distinct().ToList();
        var accounts = (await users.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id);
        var stores = (await merchants.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id);
        var photos = (await images.GetListAsync(x => ids.Contains(x.MerchantId) && x.ImageType == "store"))
            .GroupBy(x => x.MerchantId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.DisplayOrder).Select(x => x.SecureUrl).ToList());
        foreach (var dto in rows)
        {
            var customer = accounts.GetValueOrDefault(dto.CustomerId);
            dto.CustomerEmail = customer?.Email; dto.CustomerPhoneNumber = customer?.Phone; dto.CustomerAvatarUrl = customer?.Avatar;
            if (dto.MerchantId is { } merchant)
            {
                dto.MerchantEmail = accounts.GetValueOrDefault(merchant)?.Email;
                dto.MerchantPhone = stores.GetValueOrDefault(merchant)?.Phone;
                dto.MerchantImageUrls = photos.GetValueOrDefault(merchant) ?? [];
            }
            dto.CanResolve = dto.CanReject = dto.Status == IncidentStatus.Pending;
            dto.CanConfirmCompensation = dto.Status == IncidentStatus.Resolved && dto.CompensationStatus == CompensationStatus.Pending;
        }
    }
}
