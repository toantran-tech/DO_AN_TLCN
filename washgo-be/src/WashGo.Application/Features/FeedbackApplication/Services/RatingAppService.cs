using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using System.Text.Json;
using System.Net;
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
using WashGo.Application.Features.NotificationApplication.Services;
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
        private readonly NotificationPublisher _notifications;

        public RatingAppService(
            IRepository<OrderRating, Guid> ratingRepo,
            IWashOrderRepository orderRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IConfiguration configuration,
            NotificationPublisher notifications)
        {
            _ratingRepo = ratingRepo;
            _orderRepo = orderRepo;
            _merchantRepo = merchantRepo;
            _configuration = configuration;
            _notifications = notifications;
        }

        private static bool ValidStars(int? stars) => stars is null or (>= 1 and <= 5);

        private const string CreateModule = "RatingCreate";

        public async Task<BaseResponse<CustomerRatingDto>> CreateAsync(CreateRatingCommand command)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<CustomerRatingDto>("Không xác định được tài khoản khách hàng.");
            var customerId = CurrentUser.Id.Value;
            if (command.OverallStars is not (>= 1 and <= 5))
                return BadRequest<CustomerRatingDto>("Vui lòng chọn số sao đánh giá.");
            if (!ValidStars(command.QualityStars) || !ValidStars(command.SpeedStars) ||
                !ValidStars(command.AttitudeStars) || !ValidStars(command.PackagingStars))
                return BadRequest<CustomerRatingDto>("Điểm tiêu chí phải từ 1 đến 5 sao.");
            var comment = string.IsNullOrWhiteSpace(command.Comment) ? null : command.Comment.Trim();
            if (comment?.Length > 1000)
                return BadRequest<CustomerRatingDto>("Nhận xét tối đa 1000 ký tự.");
            var imageError = UploadStorage.ValidateOwnedImages(_configuration, command.ImageUrls, customerId, 0, out var images);
            if (imageError != null)
                return BadRequest<CustomerRatingDto>(imageError);
            var videoError = UploadStorage.ValidateOwnedVideos(_configuration, command.VideoUrls, customerId, out var videos);
            if (videoError != null)
                return BadRequest<CustomerRatingDto>(videoError);

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<CustomerRatingDto>(command.OperationId, customerId, CreateModule, "Create", command.OrderId.ToString(), command) is { } replay)
                return replay;
            // Locking the order serializes double submits before the unique index would reject them.
            var order = await _orderRepo.GetForUpdateAsync(command.OrderId);
            if (order == null || order.CustomerId != customerId)
                return NotFound<CustomerRatingDto>("Không tìm thấy đơn hàng của khách hàng.");
            if (order.Status != WashOrderStatus.Completed)
                return BadRequest<CustomerRatingDto>("Chỉ đánh giá được đơn hàng đã hoàn thành.");
            if (await _ratingRepo.AnyAsync(x => x.OrderId == order.Id))
                return AdministrativeWriteGuard.Error<CustomerRatingDto>(HttpStatusCode.Conflict, "AlreadyRated", "Bạn đã đánh giá đơn hàng này rồi.");

            // The Merchant that did the work comes from the order, never from the customer.
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
                ImageUrls = images,
                VideoUrls = videos
            };
            await _ratingRepo.InsertAsync(rating, autoSave: true);
            await RefreshMerchantRatingAsync(order.MerchantId);
            await _notifications.RatingCreatedAsync(rating);
            var response = Created(ToCustomerDto(rating), "Cảm ơn bạn đã đánh giá!");
            await Writes.RecordAsync(command.OperationId, customerId, CreateModule, "Create", command.OrderId.ToString(), command, response);
            await uow.CompleteAsync();
            return response;
        }

        public async Task<BaseResponse<AdministrativeOperationDto>> GetCreateOperationAsync(Guid operationId)
        {
            var row = CurrentUser.Id.HasValue ? await Writes.FindAsync(operationId, CurrentUser.Id.Value, CreateModule) : null;
            if (row == null)
                return NotFound<AdministrativeOperationDto>("Chưa có kết quả cho mã thao tác này; kiểm tra lại bằng GET /rating/order/{orderId}.");
            return Success(new AdministrativeOperationDto
            {
                OperationId = row.Id, Action = row.Action, ResourceId = row.ResourceId, CompletedAt = row.CompletedAt, State = "Completed",
                Result = JsonSerializer.Deserialize<JsonElement>(row.ResponseJson)
            });
        }

        public async Task<BaseResponse<CustomerRatingDto?>> GetByOrderAsync(Guid orderId)
        {
            var order = await _orderRepo.FindAsync(orderId);
            if (order == null || order.CustomerId != CurrentUser.Id)
                return NotFound<CustomerRatingDto?>("Không tìm thấy đơn hàng của khách hàng.");
            var rating = await _ratingRepo.FirstOrDefaultAsync(x => x.OrderId == orderId);
            return Success(rating == null ? null : ToCustomerDto(rating),
                rating == null ? "Đơn hàng chưa được đánh giá." : null);
        }

        private bool IsAdmin => CurrentUser.IsInRole("Admin");

        private async Task<(IQueryable<OrderRating>? Query, string? Error)> FilterAsync(RatingFilterQuery query)
        {
            if (query.Stars is < 1 or > 5) return (null, "Số sao lọc phải từ 1 đến 5.");
            if (query.DateRangeError() is { } dateError) return (null, dateError);
            if (query.Keyword?.Trim().Length > 100) return (null, "Từ khóa tối đa 100 ký tự.");
            var queryable = (await _ratingRepo.GetQueryableAsync()).Where(x => !x.IsDeleted);
            if (!IsAdmin)
                // Merchants only see their own visible ratings and cannot widen the scope with filters.
                queryable = queryable.Where(x => x.MerchantId == CurrentUser.Id && !x.IsHidden);
            else
            {
                if (query.MerchantId.HasValue) queryable = queryable.Where(x => x.MerchantId == query.MerchantId.Value);
                if (query.IsHidden.HasValue) queryable = queryable.Where(x => x.IsHidden == query.IsHidden.Value);
                if (query.OrderId.HasValue) queryable = queryable.Where(x => x.OrderId == query.OrderId.Value);
                if (query.CustomerId.HasValue) queryable = queryable.Where(x => x.CustomerId == query.CustomerId.Value);
            }
            if (query.Stars.HasValue)
                queryable = queryable.Where(x => x.OverallStars == query.Stars.Value);
            if (query.FromDate > 0)
                queryable = queryable.Where(x => x.CreatedOn >= query.FromDate);
            if (query.ToDate > 0)
                queryable = queryable.Where(x => x.CreatedOn <= query.ToDate);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                var admin = IsAdmin;
                queryable = queryable.Where(x => admin && x.OrderCode.ToLower().Contains(kw) ||
                    (x.Comment != null && x.Comment.ToLower().Contains(kw)) ||
                    (admin && x.CustomerName.ToLower().Contains(kw)));
            }
            return (queryable, null);
        }

        public async Task<PaginatedResult<RatingResultDto>> GetListAsync(RatingFilterQuery query)
        {
            var (queryable, error) = await FilterAsync(query);
            if (queryable == null)
                return new PaginatedResult<RatingResultDto> { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = error };
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(queryable.OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.Id)
                .Skip((page - 1) * take).Take(take));
            var dtos = items.Select(x => IsAdmin ? ToAdminDto(x) : ToPartnerDto(x)).ToList();
            return Success(dtos, total, page, take, total == 0 ? "Chưa có đánh giá nào." : null);
        }

        public async Task<BaseResponse<RatingResultDto>> GetByIdAsync(Guid id)
        {
            var rating = await _ratingRepo.FindAsync(id);
            return rating == null || rating.IsDeleted ? NotFound<RatingResultDto>("Không tìm thấy đánh giá!") : Success(ToAdminDto(rating));
        }

        public async Task<BaseResponse<RatingSummaryDto>> GetSummaryAsync(RatingFilterQuery query)
        {
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var (queryable, error) = await FilterAsync(query);
            if (queryable == null) return BadRequest<RatingSummaryDto>(error!);
            var groups = await AsyncExecuter.ToListAsync(queryable.GroupBy(x => new { x.OverallStars, x.IsHidden })
                .Select(g => new { g.Key.OverallStars, g.Key.IsHidden, Count = g.Count() }));
            var visible = queryable.Where(x => !x.IsHidden);
            var visibleCount = await AsyncExecuter.CountAsync(visible);
            var avg = visibleCount == 0 ? (decimal?)null : await AsyncExecuter.AverageAsync(visible, x => (decimal)x.OverallStars);
            await snapshot.CompleteAsync();
            return Success(new RatingSummaryDto
            {
                Total = groups.Sum(x => x.Count),
                Visible = groups.Where(x => !x.IsHidden).Sum(x => x.Count),
                Hidden = groups.Where(x => x.IsHidden).Sum(x => x.Count),
                Histogram = Enumerable.Range(1, 5).ToDictionary(s => s.ToString(), s => groups.Where(x => x.OverallStars == s).Sum(x => x.Count)),
                VisibleAverage = avg.HasValue ? decimal.Round(avg.Value, 1, MidpointRounding.AwayFromZero) : null,
                VisibleCount = visibleCount,
                GeneratedAt = DateTime.UtcNow
            });
        }

        public async Task<BaseResponse<RatingResultDto>> SetVisibilityAsync(Guid id, SetRatingVisibilityCommand command)
        {
            var reason = command.Reason?.Trim();
            if (command.IsHidden && (string.IsNullOrWhiteSpace(reason) || reason.Length > 500))
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.BadRequest, "ReasonRequired", "Vui lòng nhập lý do ẩn đánh giá (1-500 ký tự).");
            if (!command.IsHidden && reason?.Length > 500)
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.BadRequest, "ReasonTooLong", "Lý do tối đa 500 ký tự.");
            var payload = new { id, command.IsHidden, Reason = reason, command.ExpectedVersion };

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<RatingResultDto>(command.OperationId, CurrentUser.Id!.Value, "Rating", "Visibility", id.ToString(), payload) is { } replay)
                return replay;
            var rating = await _ratingRepo.FindAsync(id);
            if (rating == null || rating.IsDeleted)
                return NotFound<RatingResultDto>("Không tìm thấy đánh giá!");
            // Merchant aggregate lock first, then the rating row: the same order as rating creation.
            if (rating.MerchantId.HasValue) await Writes.LockAsync($"merchant-rating:{rating.MerchantId}");
            await Writes.LockAsync($"rating:{id}");
            rating = await _ratingRepo.GetAsync(id);
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion))
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != Version(rating))
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Đánh giá đã thay đổi; hãy tải lại.", ToAdminDto(rating));

            BaseResponse<RatingResultDto> response;
            if (rating.IsHidden == command.IsHidden)
                response = AdministrativeBatchExecutor.Unchanged(Success(ToAdminDto(rating), command.IsHidden ? "Đánh giá đã ẩn." : "Đánh giá đang hiển thị."));
            else
            {
                var before = new { rating.IsHidden, rating.HiddenReason };
                rating.IsHidden = command.IsHidden;
                rating.HiddenReason = command.IsHidden ? reason : null;
                rating.HiddenBy = command.IsHidden ? CurrentUser.Id : null;
                // PostgreSQL keeps microseconds: truncate so the version computed now equals the one read back later.
                var now = DateTime.UtcNow; now = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
                rating.HiddenAt = command.IsHidden ? now : null;
                rating.ModeratedAt = now;
                rating.ModeratedBy = CurrentUser.Id;
                await _ratingRepo.UpdateAsync(rating, autoSave: true);
                await RefreshMerchantRatingAsync(rating.MerchantId, locked: true);
                // The audit keeps the previous hide reason even though the row clears it when shown again.
                await Audit.LogAsync(new AuditEntry("Rating", command.IsHidden ? "Hide" : "Show", "OrderRating", rating.Id.ToString(), rating.OrderCode)
                {
                    Reason = reason ?? before.HiddenReason, ChangedFields = ["isHidden"],
                    Details = new { before, after = new { rating.IsHidden, rating.HiddenReason }, operationId = command.OperationId }
                });
                response = Success(ToAdminDto(rating), command.IsHidden ? "Đã ẩn đánh giá." : "Đã hiển thị lại đánh giá.");
            }
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Rating", "Visibility", id.ToString(), payload, response);
            await uow.CompleteAsync();
            return response;
        }

        public async Task<BaseResponse<AdministrativeBatchResultDto>> BatchAsync(RatingBatchCommand command)
        {
            var reason = command.Reason?.Trim();
            if (command.Action == "Hide" && (string.IsNullOrWhiteSpace(reason) || reason.Length > 500))
                return AdministrativeWriteGuard.Error<AdministrativeBatchResultDto>(HttpStatusCode.BadRequest, "ReasonRequired", "Ẩn hàng loạt cần một lý do chung 1-500 ký tự.");
            var batch = new WashGo.Application.Contracts.Features.AuditContracts.Commands.AdministrativeBatchCommand
            {
                OperationId = command.OperationId, Action = command.Action, Items = command.Items ?? [], Reason = reason
            };
            return await LazyServiceProvider.LazyGetRequiredService<AdministrativeBatchExecutor>().RunAsync("Rating", batch, 50,
                async (item, key) => AdministrativeBatchExecutor.Item(item.Id, await SetVisibilityAsync(item.Id,
                    new SetRatingVisibilityCommand { IsHidden = command.Action == "Hide", Reason = reason, ExpectedVersion = item.ExpectedVersion, OperationId = key })),
                ["Hide", "Show"]);
        }

        // Older orders never stored the Merchant name, so fall back to the Merchant record.
        private async Task<string?> GetMerchantNameAsync(Guid? merchantId, string? snapshot) =>
            !string.IsNullOrWhiteSpace(snapshot) || !merchantId.HasValue ? snapshot
                : (await _merchantRepo.FindAsync(merchantId.Value))?.BusinessName;

        /// <summary>
        /// Public Merchant score = AVG(OverallStars) of visible ratings, 1 decimal, half away from zero; computed with AVG/COUNT in
        /// the database under the Merchant rating lock. The stored 5.0 for "no rating yet" is legacy: DTOs expose rating = null.
        /// </summary>
        private async Task RefreshMerchantRatingAsync(Guid? merchantId, bool locked = false)
        {
            if (!merchantId.HasValue) return;
            if (!locked) await Writes.LockAsync($"merchant-rating:{merchantId}");
            var merchant = await _merchantRepo.FindAsync(merchantId.Value);
            if (merchant == null) return;
            var visible = (await _ratingRepo.GetQueryableAsync()).Where(x => x.MerchantId == merchantId && !x.IsHidden && !x.IsDeleted);
            var count = await AsyncExecuter.CountAsync(visible);
            merchant.Rating = count == 0 ? 5.0m
                : decimal.Round(await AsyncExecuter.AverageAsync(visible, x => (decimal)x.OverallStars), 1, MidpointRounding.AwayFromZero);
            await _merchantRepo.UpdateAsync(merchant, autoSave: true);
        }

        private static string Version(OrderRating x) => AdministrativeWriteGuard.Version(new { x.Id, x.IsHidden, x.HiddenReason, x.ModeratedAt });

        private static CustomerRatingDto ToCustomerDto(OrderRating x) => new()
        {
            Id = x.Id, OrderId = x.OrderId, OrderCode = x.OrderCode, OverallStars = x.OverallStars,
            QualityStars = x.QualityStars, SpeedStars = x.SpeedStars, AttitudeStars = x.AttitudeStars, PackagingStars = x.PackagingStars,
            Comment = x.Comment, ImageUrls = new List<string>(x.ImageUrls), VideoUrls = new List<string>(x.VideoUrls),
            IsVisible = !x.IsHidden, CreatedOn = x.CreatedOn
        };

        private static RatingResultDto ToAdminDto(OrderRating x) => new()
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
            VideoUrls = new List<string>(x.VideoUrls),
            IsHidden = x.IsHidden,
            HiddenReason = x.HiddenReason,
            HiddenAt = x.HiddenAt,
            CreatedOn = x.CreatedOn,
            Version = Version(x),
            CanHide = !x.IsHidden,
            CanShow = x.IsHidden,
            ActionReasonCode = x.IsHidden ? "AlreadyHidden" : "AlreadyVisible",
            ModeratedAt = x.ModeratedAt,
            ModeratedBy = x.ModeratedBy,
            ContentReleased = !x.IsHidden && x.ModeratedAt != null
        };

        /// <summary>Partner view: no customer/order identity; free text and media only after an Admin reviewed and kept it visible.</summary>
        private static RatingResultDto ToPartnerDto(OrderRating x)
        {
            var released = !x.IsHidden && x.ModeratedAt != null;
            return new RatingResultDto
            {
                Id = x.Id, MerchantId = x.MerchantId, MerchantName = x.MerchantName, OverallStars = x.OverallStars,
                QualityStars = x.QualityStars, SpeedStars = x.SpeedStars, AttitudeStars = x.AttitudeStars, PackagingStars = x.PackagingStars,
                Comment = released ? x.Comment : null,
                ImageUrls = released ? new List<string>(x.ImageUrls) : [],
                VideoUrls = released ? new List<string>(x.VideoUrls) : [],
                CreatedOn = x.CreatedOn, ContentReleased = released
            };
        }
    }
}
