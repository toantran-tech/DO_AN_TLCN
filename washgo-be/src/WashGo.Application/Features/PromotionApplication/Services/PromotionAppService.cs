using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.PromotionContracts.Commands;
using WashGo.Application.Contracts.Features.PromotionContracts.Queries;
using WashGo.Application.Contracts.Features.PromotionContracts.Results;
using WashGo.Application.Contracts.Features.PromotionContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Promotion;
using WashGo.Domain.Entities.Promotion.Interfaces;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PromotionApplication.Services
{
    public class PromotionAppService : WashGoAppService, IPromotionAppService
    {
        private static readonly Regex CodePattern = new("^[A-Z0-9_-]{3,50}$", RegexOptions.Compiled);
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        private readonly IPromotionRepository _promotionRepo;
        private readonly IRepository<PromotionUsage, Guid> _usageRepo;
        private readonly IWashOrderRepository _orderRepo;

        public PromotionAppService(
            IPromotionRepository promotionRepo,
            IRepository<PromotionUsage, Guid> usageRepo,
            IWashOrderRepository orderRepo)
        {
            _promotionRepo = promotionRepo;
            _usageRepo = usageRepo;
            _orderRepo = orderRepo;
        }

        // JSON without an offset is treated as UTC so PostgreSQL timestamptz never receives an unspecified kind.
        private static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        public async Task<PaginatedResult<PromotionResultDto>> GetListAsync(PromotionFilterQuery query)
        {
            var now = DateTime.UtcNow;
            var queryable = (await _promotionRepo.GetQueryableAsync()).Where(x => !x.IsDeleted);

            if (query.DiscountType.HasValue)
                queryable = queryable.Where(x => x.DiscountType == query.DiscountType.Value);

            // Mirrors Promotion.GetStatus so the filter matches the status shown in each row.
            queryable = query.Status switch
            {
                PromotionStatus.Inactive => queryable.Where(x => !x.IsActive),
                PromotionStatus.Upcoming => queryable.Where(x => x.IsActive && x.StartAt > now),
                PromotionStatus.Expired => queryable.Where(x => x.IsActive && x.StartAt <= now && x.EndAt < now),
                PromotionStatus.Exhausted => queryable.Where(x => x.IsActive && x.StartAt <= now && x.EndAt >= now
                    && x.UsageLimit != null && x.UsedCount >= x.UsageLimit),
                PromotionStatus.Active => queryable.Where(x => x.IsActive && x.StartAt <= now && x.EndAt >= now
                    && (x.UsageLimit == null || x.UsedCount < x.UsageLimit)),
                _ => queryable
            };

            if (query.FromDate > 0)
            {
                var from = DateTimeOffset.FromUnixTimeSeconds(query.FromDate).UtcDateTime;
                queryable = queryable.Where(x => x.EndAt >= from);
            }
            if (query.ToDate > 0)
            {
                var to = DateTimeOffset.FromUnixTimeSeconds(query.ToDate).UtcDateTime;
                queryable = queryable.Where(x => x.StartAt <= to);
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                queryable = queryable.Where(x => x.Code.ToLower().Contains(kw) ||
                                                 (x.Description != null && x.Description.ToLower().Contains(kw)));
            }

            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(
                queryable.OrderByDescending(x => x.CreatedOn).ThenBy(x => x.Code)
                         .Skip((page - 1) * take)
                         .Take(take));

            var dtos = items.Select(x => ToDto(x, now)).ToList();
            return Success(dtos, total, page, take,
                total == 0 ? "Chưa có mã giảm giá nào." : null);
        }

        public async Task<BaseResponse<PromotionResultDto>> GetByIdAsync(Guid id)
        {
            var entity = await FindActiveAsync(id);
            if (entity == null)
                return NotFound<PromotionResultDto>("Không tìm thấy mã giảm giá!");
            return Success(ToDto(entity, DateTime.UtcNow));
        }

        public async Task<BaseResponse<PromotionResultDto>> CreateAsync(CreatePromotionCommand command)
        {
            var code = Promotion.NormalizeCode(command.Code);
            var error = Validate(command, code);
            if (error != null)
                return BadRequest<PromotionResultDto>(error);
            if (await _promotionRepo.AnyAsync(x => x.Code == code && !x.IsDeleted))
                return BadRequest<PromotionResultDto>("Mã giảm giá này đã tồn tại.");

            var entity = new Promotion { Code = code };
            Apply(command, entity);
            await _promotionRepo.InsertAsync(entity, autoSave: true);
            return Created(ToDto(entity, DateTime.UtcNow), "Thêm Khuyến mãi thành công!");
        }

        public async Task<BaseResponse<PromotionResultDto>> UpdateAsync(UpdatePromotionCommand command)
        {
            var entity = await FindActiveAsync(command.Id);
            if (entity == null)
                return NotFound<PromotionResultDto>("Không tìm thấy mã giảm giá để cập nhật!");

            var code = Promotion.NormalizeCode(command.Code);
            var error = Validate(command, code);
            if (error != null)
                return BadRequest<PromotionResultDto>(error);
            if (code != entity.Code)
            {
                if (await _usageRepo.AnyAsync(x => x.PromotionId == entity.Id))
                    return BadRequest<PromotionResultDto>("Không thể đổi mã của khuyến mãi đã có lượt sử dụng.");
                if (await _promotionRepo.AnyAsync(x => x.Code == code && x.Id != entity.Id && !x.IsDeleted))
                    return BadRequest<PromotionResultDto>("Mã giảm giá này đã tồn tại.");
                entity.Code = code;
            }
            if (command.UsageLimit.HasValue && command.UsageLimit.Value < entity.UsedCount)
                return BadRequest<PromotionResultDto>($"Tổng lượt dùng không được nhỏ hơn số lượt đã dùng ({entity.UsedCount}).");

            // UsedCount is never written here; only the order flow changes it atomically.
            Apply(command, entity);
            await _promotionRepo.UpdateAsync(entity, autoSave: true);
            return Success(ToDto(entity, DateTime.UtcNow), "Cập nhật Khuyến mãi thành công!");
        }

        public async Task<BaseResponse<bool>> DeleteAsync(Guid id)
        {
            var entity = await FindActiveAsync(id);
            if (entity == null)
                return NotFound<bool>("Không tìm thấy mã giảm giá!");
            if (entity.UsedCount > 0 || await _usageRepo.AnyAsync(x => x.PromotionId == id && !x.IsReverted))
                return BadRequest<bool>("Không thể xóa mã đã có lượt sử dụng.");

            // Soft delete keeps the record for audit; the filtered unique index lets the code be reused.
            entity.IsDeleted = true;
            entity.IsActive = false;
            entity.DeletedOn = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            entity.DeletedBy = CurrentUser.Id?.ToString();
            await _promotionRepo.UpdateAsync(entity, autoSave: true);
            return Success(true, "Xóa Khuyến mãi thành công!");
        }

        public async Task<BaseResponse<bool>> ToggleActiveAsync(Guid id)
        {
            var entity = await FindActiveAsync(id);
            if (entity == null)
                return NotFound<bool>("Không tìm thấy mã giảm giá!");

            entity.IsActive = !entity.IsActive;
            await _promotionRepo.UpdateAsync(entity, autoSave: true);
            return Success(entity.IsActive, $"Đã {(entity.IsActive ? "kích hoạt" : "ngừng áp dụng")} mã giảm giá.");
        }

        public async Task<BaseResponse<PromotionStatisticsDto>> GetStatisticsAsync(Guid id, long? fromDate, long? toDate)
        {
            var entity = await FindActiveAsync(id);
            if (entity == null)
                return NotFound<PromotionStatisticsDto>("Không tìm thấy mã giảm giá!");

            var stats = await BuildStatisticsAsync(entity, fromDate, toDate);
            return Success(stats, stats.UsedCount == 0 ? "Mã này chưa được sử dụng lần nào." : null);
        }

        public async Task<BaseResponse<string>> ExportStatisticsCsvAsync(Guid id, long? fromDate, long? toDate)
        {
            var entity = await FindActiveAsync(id);
            if (entity == null)
                return NotFound<string>("Không tìm thấy mã giảm giá!");

            var stats = await BuildStatisticsAsync(entity, fromDate, toDate);
            var csv = new StringBuilder();
            csv.AppendLine("Ngay,SoLuotDung,TongTienGiam,DoanhThu");
            foreach (var day in stats.Daily)
                csv.AppendLine(string.Join(',', day.Date, day.UsedCount.ToString(CultureInfo.InvariantCulture),
                    day.DiscountAmount.ToString(CultureInfo.InvariantCulture), day.Revenue.ToString(CultureInfo.InvariantCulture)));
            csv.AppendLine(string.Join(',', "TONG", stats.UsedCount.ToString(CultureInfo.InvariantCulture),
                stats.TotalDiscountAmount.ToString(CultureInfo.InvariantCulture), stats.Revenue.ToString(CultureInfo.InvariantCulture)));
            return Success(csv.ToString());
        }

        private async Task<Promotion?> FindActiveAsync(Guid id)
        {
            var entity = await _promotionRepo.FindAsync(id);
            return entity == null || entity.IsDeleted ? null : entity;
        }

        private async Task<PromotionStatisticsDto> BuildStatisticsAsync(Promotion promotion, long? fromDate, long? toDate)
        {
            var usages = await _usageRepo.GetQueryableAsync();
            var orders = await _orderRepo.GetQueryableAsync();
            var query = from usage in usages
                        join order in orders on usage.OrderId equals order.Id
                        where usage.PromotionId == promotion.Id && !usage.IsReverted
                        select new
                        {
                            usage.UsedAt,
                            usage.OrderAmount,
                            usage.DiscountAmount,
                            order.FinalAmount,
                            order.PaymentStatus
                        };
            if (fromDate is > 0)
            {
                var from = DateTimeOffset.FromUnixTimeSeconds(fromDate.Value).UtcDateTime;
                query = query.Where(x => x.UsedAt >= from);
            }
            if (toDate is > 0)
            {
                var to = DateTimeOffset.FromUnixTimeSeconds(toDate.Value).UtcDateTime;
                query = query.Where(x => x.UsedAt <= to);
            }
            var rows = await AsyncExecuter.ToListAsync(query);

            return new PromotionStatisticsDto
            {
                PromotionId = promotion.Id,
                Code = promotion.Code,
                UsedCount = rows.Count,
                TotalDiscountAmount = rows.Sum(x => x.DiscountAmount),
                TotalOrderAmount = rows.Sum(x => x.OrderAmount),
                Revenue = rows.Sum(x => x.FinalAmount),
                PaidRevenue = rows.Where(x => x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.FinalAmount),
                Daily = rows
                    .GroupBy(x => (x.UsedAt + VietnamOffset).Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new PromotionDailyUsageDto
                    {
                        Date = g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        UsedCount = g.Count(),
                        DiscountAmount = g.Sum(x => x.DiscountAmount),
                        Revenue = g.Sum(x => x.FinalAmount)
                    })
                    .ToList()
            };
        }

        private static string? Validate(CreatePromotionCommand command, string code)
        {
            if (!CodePattern.IsMatch(code))
                return "Mã giảm giá phải có 3–50 ký tự gồm chữ in hoa, số, '-' hoặc '_'.";
            if (command.Description?.Length > 500)
                return "Mô tả tối đa 500 ký tự.";
            if (!Enum.IsDefined(command.DiscountType) || command.DiscountValue <= 0 ||
                (command.DiscountType == DiscountType.Percentage && command.DiscountValue > 100) ||
                command.MinOrderAmount < 0 || command.MaxDiscountAmount is <= 0)
                return "Mức giảm không hợp lệ.";
            if (command.StartAt == default || command.EndAt == default || ToUtc(command.EndAt) <= ToUtc(command.StartAt))
                return "Thời gian kết thúc phải sau thời gian bắt đầu.";
            if (command.UsageLimit is < 1 || command.UsageLimitPerCustomer is < 1 ||
                (command.UsageLimit.HasValue && command.UsageLimitPerCustomer > command.UsageLimit))
                return "Số lượt dùng không hợp lệ.";
            return null;
        }

        private static void Apply(CreatePromotionCommand command, Promotion entity)
        {
            entity.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
            entity.DiscountType = command.DiscountType;
            entity.DiscountValue = command.DiscountValue;
            entity.MinOrderAmount = command.MinOrderAmount;
            entity.MaxDiscountAmount = command.MaxDiscountAmount;
            entity.StartAt = ToUtc(command.StartAt);
            entity.EndAt = ToUtc(command.EndAt);
            entity.UsageLimit = command.UsageLimit;
            entity.UsageLimitPerCustomer = command.UsageLimitPerCustomer;
            entity.IsActive = command.IsActive;
        }

        private static PromotionResultDto ToDto(Promotion x, DateTime now) => new()
        {
            Id = x.Id,
            Code = x.Code,
            Description = x.Description,
            DiscountType = x.DiscountType,
            DiscountValue = x.DiscountValue,
            MinOrderAmount = x.MinOrderAmount,
            MaxDiscountAmount = x.MaxDiscountAmount,
            StartAt = x.StartAt,
            EndAt = x.EndAt,
            UsageLimit = x.UsageLimit,
            UsageLimitPerCustomer = x.UsageLimitPerCustomer,
            UsedCount = x.UsedCount,
            IsActive = x.IsActive,
            Status = x.GetStatus(now),
            CreatedOn = x.CreatedOn
        };
    }
}
