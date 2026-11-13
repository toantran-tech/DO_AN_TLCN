using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using WashGo.Application.Contracts.Features.FeedbackContracts.Results;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;

namespace WashGo.Application.Features.FeedbackApplication.Services;

/// <summary>Batch-load current contacts for Admin only. Rating names remain historical snapshots.</summary>
public class RatingContactEnricher(IRepository<User, Guid> users, IRepository<Merchant, Guid> merchants,
    IRepository<MerchantImage, Guid> images, ICurrentUser currentUser) : ITransientDependency
{
    public async Task FillAsync(List<RatingResultDto> rows)
    {
        if (!currentUser.IsInRole("Admin") || rows.Count == 0) return;
        var ids = rows.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value)
            .Concat(rows.Where(x => x.MerchantId.HasValue).Select(x => x.MerchantId!.Value)).Distinct().ToList();
        var accounts = (await users.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id);
        var stores = (await merchants.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id);
        var photos = (await images.GetListAsync(x => ids.Contains(x.MerchantId) && x.ImageType == "store"))
            .GroupBy(x => x.MerchantId).ToDictionary(g => g.Key,
                g => g.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Id).Select(x => x.SecureUrl).ToList());
        var now = DateTime.UtcNow;
        foreach (var dto in rows)
        {
            var customer = dto.CustomerId is { } customerId ? accounts.GetValueOrDefault(customerId) : null;
            dto.CustomerAvatarUrl = customer?.Avatar;
            dto.CustomerEmail = customer?.Email;
            dto.CustomerPhoneNumber = customer?.Phone;
            if (dto.MerchantId is { } merchantId && stores.TryGetValue(merchantId, out var store))
            {
                dto.MerchantEmail = accounts.GetValueOrDefault(merchantId)?.Email;
                dto.MerchantPhone = store.Phone;
                dto.MerchantImageUrls = photos.GetValueOrDefault(merchantId) ?? [];
            }
            dto.ContactRetrievedAt = now;
        }
    }
}
