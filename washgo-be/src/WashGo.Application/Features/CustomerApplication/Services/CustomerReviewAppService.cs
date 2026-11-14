using System.Net;
using System.Data;
using System.Text.RegularExpressions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.CustomerContracts.Results;
using WashGo.Application.Contracts.Features.CustomerContracts.Services;
using WashGo.Application.Contracts.Features.FileContracts.Results;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.CustomerApplication.Services;

public class CustomerReviewAppService(IRepository<OrderRating, Guid> ratings,
    IRepository<WashOrder, Guid> orders, ObjectStorage objects) : WashGoAppService, ICustomerReviewAppService
{
    private static readonly Regex ImagePath = new(@"^/uploads/(?<owner>[0-9a-f]{32})/(?<file>[0-9a-f]{32}\.(jpg|png|webp))$", RegexOptions.Compiled);

    private async Task<IQueryable<OrderRating>> ReleasedAsync()
    {
        var validOrders = (await orders.GetQueryableAsync()).Where(x => !x.IsDeleted && x.Status == WashOrderStatus.Completed);
        return (await ratings.GetQueryableAsync()).Where(x => !x.IsDeleted && !x.IsHidden &&
            x.ContentApprovedAt != null && x.PublicApprovedAt != null && x.PublicId != null &&
            x.OverallStars >= 1 && x.OverallStars <= 5 && validOrders.Any(o => o.Id == x.OrderId));
    }

    public async Task<BaseResponse<CustomerReviewPageDto>> GetAsync(int page, int take, int? stars)
    {
        if (!CurrentUser.IsInRole("Customer")) return Forbidden<CustomerReviewPageDto>();
        if (page < 1 || take is < 1 or > 20 || stars is < 1 or > 5 || (long)(page - 1) * take > int.MaxValue)
            return BadRequest<CustomerReviewPageDto>("Page từ 1, take 1–20, stars 1–5.");
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.RepeatableRead);
        var query = await ReleasedAsync();
        if (stars.HasValue) query = query.Where(x => x.OverallStars == stars.Value);
        var total = await AsyncExecuter.CountAsync(query);
        var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.PublicApprovedAt).ThenBy(x => x.PublicId)
            .Skip((page - 1) * take).Take(take));
        var result = new CustomerReviewPageDto { Total = total, Page = page, Take = take };
        foreach (var row in rows)
        {
            var images = new List<string>();
            for (var index = 0; index < row.ImageUrls.Count; index++)
            {
                var key = SafeImageKey(row, index);
                if (key != null && await objects.ExistsAsync(key)) images.Add($"/customer/reviews/{row.PublicId}/images/{index}");
            }
            result.Items.Add(new CustomerReviewDto { Id = row.PublicId!.Value, OverallStars = row.OverallStars,
                Comment = row.Comment, PublishedAt = row.PublicApprovedAt!.Value, ImageUrls = images });
        }
        await uow.CompleteAsync();
        return Success(result);
    }

    public async Task<BaseResponse<CustomerReviewSummaryDto>> SummaryAsync()
    {
        if (!CurrentUser.IsInRole("Customer")) return Forbidden<CustomerReviewSummaryDto>();
        var groups = await AsyncExecuter.ToListAsync((await ReleasedAsync()).GroupBy(x => x.OverallStars)
            .Select(g => new { Stars = g.Key, Count = g.Count() }));
        var total = groups.Sum(x => x.Count);
        return Success(new CustomerReviewSummaryDto
        {
            Total = total,
            AverageStars = total == 0 ? null : decimal.Round(groups.Sum(x => (decimal)x.Stars * x.Count) / total, 2),
            Distribution = Enumerable.Range(1, 5).Select(stars =>
            {
                var count = groups.FirstOrDefault(x => x.Stars == stars)?.Count ?? 0;
                return new CustomerReviewDistributionDto { Stars = stars, Count = count, Rate = total == 0 ? 0 : decimal.Round(100m * count / total, 2) };
            }).ToList()
        });
    }

    public async Task<BaseResponse<PrivateDocumentDownloadDto>> GetImageAsync(Guid publicId, int index)
    {
        if (!CurrentUser.IsInRole("Customer")) return Forbidden<PrivateDocumentDownloadDto>();
        var rating = await AsyncExecuter.FirstOrDefaultAsync((await ReleasedAsync()).Where(x => x.PublicId == publicId));
        var path = rating == null ? null : SafeImageKey(rating, index);
        if (path == null) return NotFound<PrivateDocumentDownloadDto>("Không tìm thấy ảnh đã phát hành.");
        try
        {
            var bytes = await objects.ReadAsync(path, UploadStorage.MaxImageBytes);
            if (bytes == null) return NotFound<PrivateDocumentDownloadDto>("Không tìm thấy ảnh đã phát hành.");
            return Success(new PrivateDocumentDownloadDto { Bytes = bytes, FileName = "review" + Path.GetExtension(path),
                ContentType = Path.GetExtension(path) switch { ".jpg" => "image/jpeg", ".png" => "image/png", _ => "image/webp" } });
        }
        catch (StorageUnavailableException) { throw; }
        catch (IOException) { return NotFound<PrivateDocumentDownloadDto>("Không tìm thấy ảnh đã phát hành."); }
    }

    private static string? SafeImageKey(OrderRating rating, int index)
    {
        if (index < 0 || index >= rating.ImageUrls.Count) return null;
        var match = ImagePath.Match(rating.ImageUrls[index]);
        if (!match.Success || match.Groups["owner"].Value != rating.CustomerId.ToString("N")) return null;
        return match.Groups["owner"].Value + "/" + match.Groups["file"].Value;
    }

    private static BaseResponse<T> Forbidden<T>() => new() { Succeeded = false, StatusCode = HttpStatusCode.Forbidden };
}
