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
using System.Data;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;

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
            var (imageError, images) = await UploadStorage.ValidateOwnedImagesAsync(LazyServiceProvider.LazyGetRequiredService<ObjectStorage>(), command.ImageUrls, customerId, 0);
            if (imageError != null)
                return BadRequest<CustomerRatingDto>(imageError);
            var (videoError, videos) = await UploadStorage.ValidateOwnedVideosAsync(LazyServiceProvider.LazyGetRequiredService<ObjectStorage>(), command.VideoUrls, customerId);
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
            await AdjustMerchantRatingAsync(order.MerchantId, rating.OverallStars, 1);
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
        private RatingContactEnricher Contacts => LazyServiceProvider.LazyGetRequiredService<RatingContactEnricher>();
        private async Task<RatingResultDto> AdminDtoAsync(OrderRating rating)
        {
            var dto = ToAdminDto(rating);
            await Contacts.FillAsync([dto]);
            return dto;
        }

        private async Task<OrderRating?> LockRatingAsync(Guid id)
        {
            // Project before taking locks to avoid tracking a stale rating while waiting for another writer.
            var merchantId = await AsyncExecuter.FirstOrDefaultAsync((await _ratingRepo.GetQueryableAsync())
                .Where(x => x.Id == id && !x.IsDeleted).Select(x => x.MerchantId));
            if (merchantId.HasValue) await Writes.LockAsync($"merchant-rating:{merchantId}");
            await Writes.LockAsync($"rating:{id}");
            var rating = await _ratingRepo.FindAsync(id);
            return rating is { IsDeleted: false } ? rating : null;
        }

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
                // Apply the same release rule as ToPartnerDto before counting or paging.
                queryable = queryable.Where(x => (admin && x.OrderCode.ToLower().Contains(kw)) ||
                    ((admin || (!x.IsHidden && x.ContentApprovedAt != null)) &&
                        x.Comment != null && x.Comment.ToLower().Contains(kw)) ||
                    (admin && x.CustomerName.ToLower().Contains(kw)));
            }
            return (queryable, null);
        }

        public async Task<PaginatedResult<PartnerRatingDto>> GetPartnerListAsync(RatingFilterQuery query)
        {
            var (queryable, error) = await FilterAsync(query);
            if (queryable == null)
                return new PaginatedResult<PartnerRatingDto> { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = error };
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(queryable.OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.Id)
                .Skip((page - 1) * take).Take(take));
            return Success(items.Select(ToPartnerDto).ToList(), total, page, take, total == 0 ? "Chưa có đánh giá nào." : null);
        }

        public async Task<PaginatedResult<RatingResultDto>> GetListAsync(RatingFilterQuery query)
        {
            if (!IsAdmin) return new PaginatedResult<RatingResultDto> { Succeeded = false, StatusCode = HttpStatusCode.Forbidden };
            var (queryable, error) = await FilterAsync(query);
            if (queryable == null)
                return new PaginatedResult<RatingResultDto> { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = error };
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(queryable.OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.Id)
                .Skip((page - 1) * take).Take(take));
            var dtos = items.Select(ToAdminDto).ToList();
            await Contacts.FillAsync(dtos);
            return Success(dtos, total, page, take, total == 0 ? "Chưa có đánh giá nào." : null);
        }

        public async Task<BaseResponse<RatingResultDto>> GetByIdAsync(Guid id)
        {
            if (!IsAdmin) return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.Forbidden, "AdminOnly", "Chỉ Admin được quản lý đánh giá.");
            var rating = await _ratingRepo.FindAsync(id);
            return rating == null || rating.IsDeleted ? NotFound<RatingResultDto>("Không tìm thấy đánh giá!") : Success(await AdminDtoAsync(rating));
        }

        public async Task<BaseResponse<RatingSummaryDto>> GetSummaryAsync(RatingFilterQuery query)
        {
            if (!IsAdmin) return AdministrativeWriteGuard.Error<RatingSummaryDto>(HttpStatusCode.Forbidden, "AdminOnly", "Chỉ Admin được quản lý đánh giá.");
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.RepeatableRead);
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
            if (!IsAdmin) return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.Forbidden, "AdminOnly", "Chỉ Admin được quản lý đánh giá.");
            var reason = command.Reason?.Trim();
            if (command.IsHidden && (string.IsNullOrWhiteSpace(reason) || reason.Length > 500))
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.BadRequest, "ReasonRequired", "Vui lòng nhập lý do ẩn đánh giá (1-500 ký tự).");
            if (!command.IsHidden && reason?.Length > 500)
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.BadRequest, "ReasonTooLong", "Lý do tối đa 500 ký tự.");
            var payload = new { id, command.IsHidden, Reason = reason, command.ExpectedVersion };

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<RatingResultDto>(command.OperationId, CurrentUser.Id!.Value, "Rating", "Visibility", id.ToString(), payload) is { } replay)
            {
                if (replay.Data != null) await Contacts.FillAsync([replay.Data]);
                return replay;
            }
            var rating = await LockRatingAsync(id);
            if (rating == null) return NotFound<RatingResultDto>("Không tìm thấy đánh giá!");
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion))
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != Version(rating))
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Đánh giá đã thay đổi; hãy tải lại.", await AdminDtoAsync(rating));

            BaseResponse<RatingResultDto> response;
            if (rating.IsHidden == command.IsHidden)
                response = AdministrativeBatchExecutor.Unchanged(Success(await AdminDtoAsync(rating), command.IsHidden ? "Đánh giá đã ẩn." : "Đánh giá đang hiển thị."));
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
                await AdjustMerchantRatingAsync(rating.MerchantId, command.IsHidden ? -rating.OverallStars : rating.OverallStars, command.IsHidden ? -1 : 1, locked: true);
                // The audit keeps the previous hide reason even though the row clears it when shown again.
                await Audit.LogAsync(new AuditEntry("Rating", command.IsHidden ? "Hide" : "Show", "OrderRating", rating.Id.ToString(), rating.OrderCode)
                {
                    Reason = reason ?? before.HiddenReason, ChangedFields = ["isHidden"],
                    Details = new { before, after = new { rating.IsHidden, rating.HiddenReason }, operationId = command.OperationId }
                });
                response = Success(await AdminDtoAsync(rating), command.IsHidden ? "Đã ẩn đánh giá." : "Đã hiển thị lại đánh giá.");
            }
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Rating", "Visibility", id.ToString(), payload, response);
            await uow.CompleteAsync();
            return response;
        }

        public async Task<BaseResponse<RatingResultDto>> PublishAsync(Guid id, PublishRatingCommand command)
        {
            if (!IsAdmin) return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.Forbidden, "AdminOnly", "Chỉ Admin được duyệt nội dung.");
            var reason = command.Reason?.Trim();
            if (reason?.Length > 500)
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.BadRequest, "ReasonTooLong", "Lý do tối đa 500 ký tự.");
            object payload = command.PublishForCustomers
                ? new { id, Reason = reason, command.ExpectedVersion, command.PublishForCustomers }
                : new { id, Reason = reason, command.ExpectedVersion };
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<RatingResultDto>(command.OperationId, CurrentUser.Id!.Value, "Rating", "Publish", id.ToString(), payload) is { } replay)
            {
                if (replay.Data != null) await Contacts.FillAsync([replay.Data]);
                return replay;
            }
            var rating = await LockRatingAsync(id);
            if (rating == null) return NotFound<RatingResultDto>("Không tìm thấy đánh giá!");
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion))
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != Version(rating))
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Đánh giá đã thay đổi; hãy tải lại.", await AdminDtoAsync(rating));
            if (rating.IsHidden)
                return AdministrativeWriteGuard.Error<RatingResultDto>(HttpStatusCode.BadRequest, "HiddenRating", "Hiển thị lại đánh giá trước khi duyệt nội dung.");
            BaseResponse<RatingResultDto> response;
            if (rating.ContentApprovedAt.HasValue && (!command.PublishForCustomers || rating.PublicApprovedAt.HasValue))
                response = AdministrativeBatchExecutor.Unchanged(Success(await AdminDtoAsync(rating), "Nội dung đã được duyệt."));
            else
            {
                var now = DateTime.UtcNow;
                rating.ContentApprovedAt ??= new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
                rating.ContentApprovedBy ??= CurrentUser.Id;
                if (command.PublishForCustomers)
                {
                    rating.PublicId ??= Guid.NewGuid();
                    rating.PublicApprovedAt = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
                    rating.PublicApprovedBy = CurrentUser.Id;
                }
                await _ratingRepo.UpdateAsync(rating, autoSave: true);
                await Audit.LogAsync(new AuditEntry("Rating", "Publish", "OrderRating", rating.Id.ToString(), rating.OrderCode)
                {
                    Reason = reason, ChangedFields = command.PublishForCustomers ? ["contentApprovedAt", "contentApprovedBy", "publicApprovedAt", "publicApprovedBy", "publicId"] : ["contentApprovedAt", "contentApprovedBy"],
                    Details = new { operationId = command.OperationId, command.PublishForCustomers }
                });
                response = Success(await AdminDtoAsync(rating), command.PublishForCustomers ? "Đã duyệt nội dung đánh giá cho Customer và Merchant." : "Đã duyệt nội dung đánh giá cho Merchant.");
            }
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Rating", "Publish", id.ToString(), payload, response);
            await uow.CompleteAsync();
            return response;
        }

        public async Task<BaseResponse<AdministrativeBatchResultDto>> BatchAsync(RatingBatchCommand command)
        {
            var reason = command.Reason?.Trim();
            if (command.Action == "Hide" && (string.IsNullOrWhiteSpace(reason) || reason.Length > 500))
                return AdministrativeWriteGuard.Error<AdministrativeBatchResultDto>(HttpStatusCode.BadRequest, "ReasonRequired", "Ẩn hàng loạt cần một lý do chung 1-500 ký tự.");
            var batch = new AdministrativeBatchCommand
            {
                OperationId = command.OperationId, Action = command.Action, Items = command.Items ?? [], Reason = reason
            };
            return await LazyServiceProvider.LazyGetRequiredService<AdministrativeBatchExecutor>().RunAsync("Rating", batch, 50,
                async (item, key) => AdministrativeBatchExecutor.Item(item.Id, command.Action == "Publish"
                    ? await PublishAsync(item.Id, new PublishRatingCommand { Reason = reason, ExpectedVersion = item.ExpectedVersion, OperationId = key })
                    : await SetVisibilityAsync(item.Id, new SetRatingVisibilityCommand { IsHidden = command.Action == "Hide", Reason = reason, ExpectedVersion = item.ExpectedVersion, OperationId = key })),
                ["Hide", "Show", "Publish"]);
        }

        // Older orders never stored the Merchant name, so fall back to the Merchant record.
        private async Task<string?> GetMerchantNameAsync(Guid? merchantId, string? snapshot) =>
            !string.IsNullOrWhiteSpace(snapshot) || !merchantId.HasValue ? snapshot
                : (await _merchantRepo.FindAsync(merchantId.Value))?.BusinessName;

        /// <summary>
        /// Public Merchant score kept as a running sum/count of visible ratings (no full recomputation per change, also inside a
        /// batch), updated under the Merchant rating lock in the same transaction as the rating change: always exact at commit.
        /// Score = Sum/Count rounded to 1 decimal half away from zero; 0 ratings keeps the legacy stored 5.0 (DTOs expose null).
        /// </summary>
        private async Task AdjustMerchantRatingAsync(Guid? merchantId, int deltaSum, int deltaCount, bool locked = false)
        {
            if (!merchantId.HasValue) return;
            if (!locked) await Writes.LockAsync($"merchant-rating:{merchantId}");
            var merchant = await _merchantRepo.FindAsync(merchantId.Value);
            if (merchant == null) return;
            merchant.PublicRatingSum += deltaSum;
            merchant.PublicRatingCount += deltaCount;
            merchant.Rating = merchant.PublicRatingCount <= 0 ? 5.0m
                : decimal.Round((decimal)merchant.PublicRatingSum / merchant.PublicRatingCount, 1, MidpointRounding.AwayFromZero);
            await _merchantRepo.UpdateAsync(merchant, autoSave: true);
        }

        private static string Version(OrderRating x) => AdministrativeWriteGuard.Version(new { x.Id, x.IsHidden, x.HiddenReason, x.ModeratedAt, x.ContentApprovedAt, x.ContentApprovedBy, x.PublicApprovedAt, x.PublicApprovedBy, x.PublicId });

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
            CanPublish = !x.IsHidden && x.ContentApprovedAt == null,
            CanPublishForCustomers = !x.IsHidden && x.PublicApprovedAt == null,
            PublicApprovedAt = x.PublicApprovedAt,
            PublishReasonCode = x.IsHidden ? "HiddenRating" : x.ContentApprovedAt != null ? "AlreadyPublished" : null,
            ContentApprovedAt = x.ContentApprovedAt,
            ContentApprovedBy = x.ContentApprovedBy,
            ActionReasonCode = x.IsHidden ? "AlreadyHidden" : "AlreadyVisible",
            ModeratedAt = x.ModeratedAt,
            ModeratedBy = x.ModeratedBy,
            ContentReleased = !x.IsHidden && x.ContentApprovedAt != null
        };

        /// <summary>Partner view: no customer/order identity; free text and media only after an Admin reviewed and kept it visible.</summary>
        private static PartnerRatingDto ToPartnerDto(OrderRating x)
        {
            var released = !x.IsHidden && x.ContentApprovedAt != null;
            return new PartnerRatingDto
            {
                Id = x.Id, OverallStars = x.OverallStars,
                QualityStars = x.QualityStars, SpeedStars = x.SpeedStars, AttitudeStars = x.AttitudeStars, PackagingStars = x.PackagingStars,
                Comment = released ? x.Comment : null,
                ImageUrls = released ? new List<string>(x.ImageUrls) : [],
                VideoUrls = released ? new List<string>(x.VideoUrls) : [],
                CreatedOn = x.CreatedOn, ContentReleased = released
            };
        }
    }
}
