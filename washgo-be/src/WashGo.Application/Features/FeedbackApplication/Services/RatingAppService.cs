using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.FeedbackContracts.Commands;
using WashGo.Application.Contracts.Features.FeedbackContracts.Queries;
using WashGo.Application.Contracts.Features.FeedbackContracts.Results;
using WashGo.Application.Contracts.Features.FeedbackContracts.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.FeedbackApplication.Services
{
    public class RatingAppService : WashGoAppService, IRatingAppService
    {
        private readonly IRepository<OrderRating, Guid> _ratingRepo;
        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IConfiguration _configuration;

        public RatingAppService(
            IRepository<OrderRating, Guid> ratingRepo,
            IWashOrderRepository orderRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IConfiguration configuration)
        {
            _ratingRepo = ratingRepo;
            _orderRepo = orderRepo;
            _merchantRepo = merchantRepo;
            _configuration = configuration;
        }

        private static bool ValidStars(int? stars) => stars is null or (>= 1 and <= 5);

        public async Task<BaseResponse<RatingResultDto>> CreateAsync(CreateRatingCommand command)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<RatingResultDto>("Không xác định được tài khoản khách hàng.");
            var customerId = CurrentUser.Id.Value;
            if (command.OverallStars is not (>= 1 and <= 5))
                return BadRequest<RatingResultDto>("Vui lòng chọn số sao đánh giá.");
            if (!ValidStars(command.QualityStars) || !ValidStars(command.SpeedStars) ||
                !ValidStars(command.AttitudeStars) || !ValidStars(command.PackagingStars))
                return BadRequest<RatingResultDto>("Điểm tiêu chí phải từ 1 đến 5 sao.");
            var comment = string.IsNullOrWhiteSpace(command.Comment) ? null : command.Comment.Trim();
            if (comment?.Length > 1000)
                return BadRequest<RatingResultDto>("Nhận xét tối đa 1000 ký tự.");
            var imageError = UploadStorage.ValidateOwnedImages(_configuration, command.ImageUrls, customerId, 0, out var images);
            if (imageError != null)
                return BadRequest<RatingResultDto>(imageError);

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            // Locking the order serializes double submits before the unique index would reject them.
            var order = await _orderRepo.GetForUpdateAsync(command.OrderId);
            if (order == null || order.CustomerId != customerId)
                return NotFound<RatingResultDto>("Không tìm thấy đơn hàng của khách hàng.");
            if (order.Status != WashOrderStatus.Completed)
                return BadRequest<RatingResultDto>("Chỉ đánh giá được đơn hàng đã hoàn thành.");
            if (await _ratingRepo.AnyAsync(x => x.OrderId == order.Id))
                return BadRequest<RatingResultDto>("Bạn đã đánh giá đơn hàng này rồi.");

            var rating = new OrderRating
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                CustomerId = customerId,
                CustomerName = order.CustomerName,
                MerchantId = order.MerchantId,
                MerchantName = await GetMerchantNameAsync(order.MerchantId, order.MerchantName),
                OverallStars = command.OverallStars.Value,
                QualityStars = command.QualityStars,
                SpeedStars = command.SpeedStars,
                AttitudeStars = command.AttitudeStars,
                PackagingStars = command.PackagingStars,
                Comment = comment,
                ImageUrls = images
            };
            await _ratingRepo.InsertAsync(rating, autoSave: true);
            await RefreshMerchantRatingAsync(order.MerchantId);
            await uow.CompleteAsync();
            return Created(ToDto(rating), "Cảm ơn bạn đã đánh giá!");
        }

        public async Task<BaseResponse<RatingResultDto?>> GetByOrderAsync(Guid orderId)
        {
            var order = await _orderRepo.FindAsync(orderId);
            if (order == null || order.CustomerId != CurrentUser.Id)
                return NotFound<RatingResultDto?>("Không tìm thấy đơn hàng của khách hàng.");
            var rating = await _ratingRepo.FirstOrDefaultAsync(x => x.OrderId == orderId);
            return Success(rating == null ? null : ToDto(rating),
                rating == null ? "Đơn hàng chưa được đánh giá." : null);
        }

        public async Task<PaginatedResult<RatingResultDto>> GetListAsync(RatingFilterQuery query)
        {
            var isMerchant = !CurrentUser.IsInRole("Admin");
            var queryable = await _ratingRepo.GetQueryableAsync();
            if (isMerchant)
                // Merchants only see their own visible ratings.
                queryable = queryable.Where(x => x.MerchantId == CurrentUser.Id && !x.IsHidden);
            else if (query.MerchantId.HasValue)
                queryable = queryable.Where(x => x.MerchantId == query.MerchantId.Value);
            if (query.IsHidden.HasValue && !isMerchant)
                queryable = queryable.Where(x => x.IsHidden == query.IsHidden.Value);
            if (query.Stars.HasValue)
                queryable = queryable.Where(x => x.OverallStars == query.Stars.Value);
            if (query.FromDate > 0)
                queryable = queryable.Where(x => x.CreatedOn >= query.FromDate);
            if (query.ToDate > 0)
                queryable = queryable.Where(x => x.CreatedOn <= query.ToDate);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                queryable = queryable.Where(x => x.OrderCode.ToLower().Contains(kw) ||
                    (x.Comment != null && x.Comment.ToLower().Contains(kw)) ||
                    (!isMerchant && x.CustomerName.ToLower().Contains(kw)));
            }

            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(queryable.OrderByDescending(x => x.CreatedOn)
                .Skip((page - 1) * take).Take(take));
            var dtos = items.Select(ToDto).ToList();
            if (isMerchant)
                dtos.ForEach(x => { x.CustomerId = Guid.Empty; x.CustomerName = "***"; });
            return Success(dtos, total, page, take, total == 0 ? "Chưa có đánh giá nào." : null);
        }

        public async Task<BaseResponse<RatingResultDto>> SetVisibilityAsync(Guid id, SetRatingVisibilityCommand command)
        {
            var reason = command.Reason?.Trim();
            if (command.IsHidden && (string.IsNullOrWhiteSpace(reason) || reason.Length > 500))
                return BadRequest<RatingResultDto>("Vui lòng nhập lý do ẩn đánh giá (tối đa 500 ký tự).");

            var rating = await _ratingRepo.FindAsync(id);
            if (rating == null)
                return NotFound<RatingResultDto>("Không tìm thấy đánh giá!");
            rating.IsHidden = command.IsHidden;
            rating.HiddenReason = command.IsHidden ? reason : null;
            rating.HiddenBy = command.IsHidden ? CurrentUser.Id : null;
            rating.HiddenAt = command.IsHidden ? DateTime.UtcNow : null;
            await _ratingRepo.UpdateAsync(rating, autoSave: true);
            await RefreshMerchantRatingAsync(rating.MerchantId);
            return Success(ToDto(rating), command.IsHidden ? "Đã ẩn đánh giá." : "Đã hiển thị lại đánh giá.");
        }

        // Older orders never stored the Merchant name, so fall back to the Merchant record.
        private async Task<string?> GetMerchantNameAsync(Guid? merchantId, string? snapshot) =>
            !string.IsNullOrWhiteSpace(snapshot) || !merchantId.HasValue ? snapshot
                : (await _merchantRepo.FindAsync(merchantId.Value))?.BusinessName;

        // Hidden ratings do not count toward the Merchant's public score.
        private async Task RefreshMerchantRatingAsync(Guid? merchantId)
        {
            if (!merchantId.HasValue) return;
            var merchant = await _merchantRepo.FindAsync(merchantId.Value);
            if (merchant == null) return;
            var stars = await AsyncExecuter.ToListAsync((await _ratingRepo.GetQueryableAsync())
                .Where(x => x.MerchantId == merchantId && !x.IsHidden).Select(x => x.OverallStars));
            merchant.Rating = stars.Count == 0 ? 5.0m
                : decimal.Round((decimal)stars.Average(), 1, MidpointRounding.AwayFromZero);
            await _merchantRepo.UpdateAsync(merchant, autoSave: true);
        }

        private static RatingResultDto ToDto(OrderRating x) => new()
        {
            Id = x.Id,
            OrderId = x.OrderId,
            OrderCode = x.OrderCode,
            CustomerId = x.CustomerId,
            CustomerName = x.CustomerName,
            MerchantId = x.MerchantId,
            MerchantName = x.MerchantName,
            OverallStars = x.OverallStars,
            QualityStars = x.QualityStars,
            SpeedStars = x.SpeedStars,
            AttitudeStars = x.AttitudeStars,
            PackagingStars = x.PackagingStars,
            Comment = x.Comment,
            ImageUrls = new List<string>(x.ImageUrls),
            IsHidden = x.IsHidden,
            HiddenReason = x.HiddenReason,
            HiddenAt = x.HiddenAt,
            CreatedOn = x.CreatedOn
        };
    }
}
