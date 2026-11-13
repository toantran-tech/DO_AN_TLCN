using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Volo.Abp.Uow;
using System.Net;
using WashGo.Application.Features.AuditApplication.Services;
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
using System.Data;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Domain.Entities.Identity;

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

        private static readonly Dictionary<string, string> SortFields = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "Id", ["Code"] = "Code", ["CreatedOn"] = "CreatedOn", ["CreatedAt"] = "CreatedOn",
            ["StartAt"] = "StartAt", ["EndAt"] = "EndAt", ["UsageLimit"] = "UsageLimit", ["UsedCount"] = "UsedCount"
        };
        private static readonly Dictionary<string, string> UsageSortFields = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "Id", ["UsedAt"] = "UsedAt", ["RevertedAt"] = "RevertedAt", ["DiscountAmount"] = "DiscountAmount", ["OrderAmount"] = "OrderAmount"
        };

        public async Task<PaginatedResult<PromotionUsageDto>> GetUsageHistoryAsync(Guid id, PromotionUsageFilterQuery query)
        {
            if (!CurrentUser.IsInRole("Admin"))
                return new() { Succeeded = false, StatusCode = HttpStatusCode.Forbidden, Message = "Chỉ Admin được xem lịch sử sử dụng voucher." };
            if (query.DateRangeError() != null || !WhitelistedQuerySort.IsValid(query.SortBy, UsageSortFields) || query.Keyword?.Length > 200)
                return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = "Bộ lọc lịch sử hoặc trường sắp xếp không hợp lệ." };
            if (!await _promotionRepo.AnyAsync(x => x.Id == id && !x.IsDeleted))
                return new() { Succeeded = false, StatusCode = HttpStatusCode.NotFound, Message = "Không tìm thấy mã giảm giá." };
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.RepeatableRead);
            var users = await LazyServiceProvider.LazyGetRequiredService<IRepository<User, Guid>>().GetQueryableAsync();
            var orders = await _orderRepo.GetQueryableAsync();
            var q = from usage in await _usageRepo.GetQueryableAsync()
                    join order in orders on usage.OrderId equals order.Id into relatedOrders
                    from order in relatedOrders.DefaultIfEmpty()
                    join user in users on usage.CustomerId equals user.Id into relatedUsers
                    from user in relatedUsers.DefaultIfEmpty()
                    where usage.PromotionId == id && !usage.IsDeleted
                    select new PromotionUsageDto { Id = usage.Id, OrderId = usage.OrderId, OrderCode = order == null ? null : order.OrderCode,
                        CustomerId = usage.CustomerId, CustomerName = user == null ? null : user.FullName,
                        OrderAmount = usage.OrderAmount, DiscountAmount = usage.DiscountAmount, UsedAt = usage.UsedAt,
                        IsReverted = usage.IsReverted, RevertedAt = usage.RevertedAt };
            if (query.OrderId.HasValue) q = q.Where(x => x.OrderId == query.OrderId);
            if (query.CustomerId.HasValue) q = q.Where(x => x.CustomerId == query.CustomerId);
            if (query.IsReverted.HasValue) q = q.Where(x => x.IsReverted == query.IsReverted);
            if (query.FromDate > 0) { var from = DateTimeOffset.FromUnixTimeSeconds(query.FromDate).UtcDateTime; q = q.Where(x => x.UsedAt >= from); }
            if (query.ToDate > 0) { var to = DateTimeOffset.FromUnixTimeSeconds(query.ToDate).UtcDateTime; q = q.Where(x => x.UsedAt <= to); }
            if (!string.IsNullOrWhiteSpace(query.Keyword)) { var term = query.Keyword.Trim().ToLower(); q = q.Where(x => x.OrderCode != null && x.OrderCode.ToLower().Contains(term) || x.CustomerName != null && x.CustomerName.ToLower().Contains(term)); }
            var total = await AsyncExecuter.CountAsync(q);
            var page = Math.Max(1, query.Page); var take = Math.Clamp(query.Take, 1, 100);
            var rows = await AsyncExecuter.ToListAsync(WhitelistedQuerySort.Apply(q, query.SortBy, UsageSortFields, new SortOption { Field = "UsedAt", IsDescending = true }).Skip((int)Math.Min(int.MaxValue, ((long)page - 1) * take)).Take(take));
            await snapshot.CompleteAsync();
            return Success(rows, total, page, take);
        }

        public async Task<PaginatedResult<PromotionResultDto>> GetListAsync(PromotionFilterQuery query)
        {
            if (query.DateRangeError() is { } dateError || query.Status.HasValue && !Enum.IsDefined(query.Status.Value) || query.DiscountType.HasValue && !Enum.IsDefined(query.DiscountType.Value))
                return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = query.DateRangeError() ?? "Bộ lọc không hợp lệ." };
            if (!WhitelistedQuerySort.IsValid(query.SortBy, SortFields) || query.Keyword?.Length > 200)
                return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = "Chỉ sắp xếp theo Code, CreatedOn, StartAt, EndAt, UsageLimit, UsedCount hoặc Id (tối đa 8 tiêu chí)." };
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
                WhitelistedQuerySort.Apply(queryable, query.SortBy, SortFields, new SortOption { Field = "CreatedOn", IsDescending = true }, new SortOption { Field = "Code" })
                         .Skip((int)Math.Min(int.MaxValue, ((long)page - 1) * take))
                         .Take(take));

            var dtos = items.Select(x => ToDto(x, now)).ToList();
            var ids = items.Select(x => x.Id).ToList();
            var historical = await AsyncExecuter.ToListAsync((await _usageRepo.GetQueryableAsync()).Where(x => ids.Contains(x.PromotionId)).Select(x => x.PromotionId).Distinct());
            foreach (var dto in dtos)
            {
                dto.CanDelete = dto.CanDelete && !historical.Contains(dto.Id);
                // Same check as UpdateAsync: any usage, even reverted, freezes the code.
                dto.CanChangeCode = !historical.Contains(dto.Id);
                dto.ChangeCodeReasonCode = dto.CanChangeCode ? null : "HasUsageHistory";
            }
            return Success(dtos, total, page, take,
                total == 0 ? "Chưa có mã giảm giá nào." : null);
        }

        public async Task<BaseResponse<PromotionResultDto>> GetByIdAsync(Guid id)
        {
            var entity = await FindActiveAsync(id);
            if (entity == null)
                return NotFound<PromotionResultDto>("Không tìm thấy mã giảm giá!");
            var dto = ToDto(entity, DateTime.UtcNow);
            var used = await _usageRepo.AnyAsync(x => x.PromotionId == id);
            dto.CanDelete &= !used;
            dto.CanChangeCode = !used;
            dto.ChangeCodeReasonCode = used ? "HasUsageHistory" : null;
            return Success(dto);
        }
        public async Task<BaseResponse<AdministrativeSummaryDto>> GetSummaryAsync()
        {
            var now = DateTime.UtcNow;
            var groups = await AsyncExecuter.ToListAsync((await _promotionRepo.GetQueryableAsync()).Where(x => !x.IsDeleted)
                .GroupBy(x => !x.IsActive ? PromotionStatus.Inactive : x.StartAt > now ? PromotionStatus.Upcoming : x.EndAt < now ? PromotionStatus.Expired :
                    x.UsageLimit != null && x.UsedCount >= x.UsageLimit ? PromotionStatus.Exhausted : PromotionStatus.Active)
                .Select(g => new { Status = g.Key, Count = g.Count() }));
            return Success(new AdministrativeSummaryDto { Total = groups.Sum(x => x.Count), Counts = groups.ToDictionary(x => x.Status.ToString(), x => x.Count), GeneratedAt = now });
        }

        public async Task<BaseResponse<PromotionResultDto>> CreateAsync(CreatePromotionCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<PromotionResultDto>(command.OperationId, CurrentUser.Id!.Value, "Promotion", "Create", "new", command) is { } replay) return replay;
            await Writes.LockAsync("promotion-admin-codes");
            var code = Promotion.NormalizeCode(command.Code);
            var error = Validate(command, code);
            if (error != null)
                return BadRequest<PromotionResultDto>(error);
            if (await _promotionRepo.AnyAsync(x => x.Code == code && !x.IsDeleted))
                return BadRequest<PromotionResultDto>("Mã giảm giá này đã tồn tại.");

            var entity = new Promotion { Code = code };
            Apply(command, entity);
            await _promotionRepo.InsertAsync(entity, autoSave: true);
            var response = Created(ToDto(entity, DateTime.UtcNow), "Thêm Khuyến mãi thành công!");
            await Audit.LogAsync(new AuditEntry("Promotion", "Create", "Promotion", entity.Id.ToString(), entity.Code));
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Promotion", "Create", "new", command, response);
            await uow.CompleteAsync(); return response;
        }

        public async Task<BaseResponse<PromotionResultDto>> UpdateAsync(UpdatePromotionCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<PromotionResultDto>(command.OperationId, CurrentUser.Id!.Value, "Promotion", "Update", command.Id.ToString(), command) is { } replay) return replay;
            await Writes.LockAsync("promotion-admin-codes");
            var entity = await _promotionRepo.GetForUpdateAsync(command.Id);
            if (entity == null || entity.IsDeleted)
                return NotFound<PromotionResultDto>("Không tìm thấy mã giảm giá để cập nhật!");
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<PromotionResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != Version(entity)) return AdministrativeWriteGuard.Error<PromotionResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Khuyến mãi đã thay đổi.");

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

            Apply(command, entity);
            await _promotionRepo.UpdateAsync(entity, autoSave: true);
            var response = Success(ToDto(entity, DateTime.UtcNow), "Cập nhật Khuyến mãi thành công!");
            await Audit.LogAsync(new AuditEntry("Promotion", "Update", "Promotion", entity.Id.ToString(), entity.Code));
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Promotion", "Update", entity.Id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        public async Task<BaseResponse<bool>> DeleteAsync(Guid id, string? expectedVersion = null, Guid? operationId = null)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var payload = new { id, expectedVersion };
            if (await Writes.ReplayAsync<bool>(operationId, CurrentUser.Id!.Value, "Promotion", "Delete", id.ToString(), payload) is { } replay) return replay;
            var entity = await _promotionRepo.GetForUpdateAsync(id);
            if (entity == null || entity.IsDeleted)
                return NotFound<bool>("Không tìm thấy mã giảm giá!");
            if (string.IsNullOrWhiteSpace(expectedVersion)) return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (expectedVersion != Version(entity)) return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.Conflict, "VersionMismatch", "Khuyến mãi đã thay đổi.");
            if (entity.UsedCount > 0 || await _usageRepo.AnyAsync(x => x.PromotionId == id))
                return BadRequest<bool>("Không thể xóa mã đã có lượt sử dụng.");

            // Soft delete keeps the record for audit; the filtered unique index lets the code be reused.
            entity.IsDeleted = true;
            entity.IsActive = false;
            entity.DeletedOn = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            entity.DeletedBy = CurrentUser.Id?.ToString();
            await _promotionRepo.UpdateAsync(entity, autoSave: true);
            var response = Success(true, "Xóa Khuyến mãi thành công!");
            await Audit.LogAsync(new AuditEntry("Promotion", "Delete", "Promotion", id.ToString(), entity.Code));
            await Writes.RecordAsync(operationId, CurrentUser.Id!.Value, "Promotion", "Delete", id.ToString(), payload, response);
            await uow.CompleteAsync(); return response;
        }

        public async Task<BaseResponse<bool>> ToggleActiveAsync(Guid id)
        {
            await Task.CompletedTask;
            return BadRequest<bool>("Dùng PUT /promotion/{id}/status với isActive đích và expectedVersion; toggle không an toàn khi retry.");
        }

        public async Task<BaseResponse<PromotionResultDto>> SetStatusAsync(Guid id, SetPromotionStatusCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<PromotionResultDto>(command.OperationId, CurrentUser.Id!.Value, "Promotion", "Status", id.ToString(), command) is { } replay) return replay;
            var entity = await _promotionRepo.GetForUpdateAsync(id);
            if (entity == null || entity.IsDeleted) return NotFound<PromotionResultDto>("Không tìm thấy mã giảm giá.");
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<PromotionResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != Version(entity)) return AdministrativeWriteGuard.Error<PromotionResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Khuyến mãi đã thay đổi.");
            var changed = entity.IsActive != command.IsActive;
            if (changed)
            {
                entity.IsActive = command.IsActive; await _promotionRepo.UpdateAsync(entity, autoSave: true);
                await Audit.LogAsync(new AuditEntry("Promotion", "Status", "Promotion", id.ToString(), entity.Code) { ChangedFields = ["isActive"] });
            }
            var response = Success(ToDto(entity, DateTime.UtcNow));
            if (!changed) AdministrativeBatchExecutor.Unchanged(response);
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Promotion", "Status", id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        public async Task<BaseResponse<PromotionStatisticsDto>> GetStatisticsAsync(Guid id, long? fromDate, long? toDate)
        {
            if (InvalidDates(fromDate, toDate)) return BadRequest<PromotionStatisticsDto>("Khoảng ngày không hợp lệ.");
            var entity = await FindActiveAsync(id);
            if (entity == null)
                return NotFound<PromotionStatisticsDto>("Không tìm thấy mã giảm giá!");

            var stats = await BuildStatisticsAsync(entity, fromDate, toDate);
            return Success(stats, stats.UsedCount == 0 ? "Mã này chưa được sử dụng lần nào." : null);
        }

        public async Task<BaseResponse<string>> ExportStatisticsCsvAsync(Guid id, long? fromDate, long? toDate)
        {
            if (InvalidDates(fromDate, toDate)) return BadRequest<string>("Khoảng ngày không hợp lệ.");
            var entity = await FindActiveAsync(id);
            if (entity == null)
                return NotFound<string>("Không tìm thấy mã giảm giá!");

            var stats = await BuildStatisticsAsync(entity, fromDate, toDate);
            var csv = new StringBuilder();
            string N(decimal v) => v.ToString(CultureInfo.InvariantCulture);
            csv.AppendLine($"MaKhuyenMai,{stats.Code}");
            csv.AppendLine($"TuNgay,{new DateTimeOffset(stats.FromDate, TimeSpan.Zero).ToOffset(VietnamOffset):yyyy-MM-ddTHH:mm:sszzz}");
            csv.AppendLine($"DenNgay,{new DateTimeOffset(stats.ToDate, TimeSpan.Zero).ToOffset(VietnamOffset):yyyy-MM-ddTHH:mm:sszzz}");
            csv.AppendLine($"TaoLuc,{new DateTimeOffset(stats.GeneratedAt, TimeSpan.Zero).ToOffset(VietnamOffset):yyyy-MM-ddTHH:mm:sszzz}");
            csv.AppendLine("MuiGio,Asia/Ho_Chi_Minh");
            csv.AppendLine($"CanCu,\"{stats.Basis}\"");
            csv.AppendLine("GhiChu,Ban tinh moi tai thoi diem tai file; khong phai ban chup cua bang dang xem");
            csv.AppendLine();
            csv.AppendLine("Ngay,SoLuotDung,TongTienGiam,TongGiaTriDon,DoanhThu,DoanhThuDaThanhToan");
            foreach (var day in stats.Daily)
                csv.AppendLine(string.Join(',', day.Date, day.UsedCount.ToString(CultureInfo.InvariantCulture),
                    N(day.DiscountAmount), N(day.TotalOrderAmount), N(day.Revenue), N(day.PaidRevenue)));
            csv.AppendLine(string.Join(',', "TONG", stats.UsedCount.ToString(CultureInfo.InvariantCulture),
                N(stats.TotalDiscountAmount), N(stats.TotalOrderAmount), N(stats.Revenue), N(stats.PaidRevenue)));
            return Success(csv.ToString());
        }

        private async Task<Promotion?> FindActiveAsync(Guid id)
        {
            var entity = await _promotionRepo.FindAsync(id);
            return entity == null || entity.IsDeleted ? null : entity;
        }

        public Task<BaseResponse<AdministrativeBatchResultDto>> BatchAsync(AdministrativeBatchCommand command) =>
            LazyServiceProvider.LazyGetRequiredService<AdministrativeBatchExecutor>().RunAsync("Promotion", command, 100, async (item, key) =>
            {
                if (command.Action == "Delete") return AdministrativeBatchExecutor.Item(item.Id, await DeleteAsync(item.Id, item.ExpectedVersion, key));
                if (!command.IsActive.HasValue) return AdministrativeBatchExecutor.Invalid(item.Id, "InvalidTargetStatus", "Status cần isActive đích.");
                return AdministrativeBatchExecutor.Item(item.Id, await SetStatusAsync(item.Id, new() { ExpectedVersion = item.ExpectedVersion, OperationId = key, IsActive = command.IsActive.Value }));
            });

        private async Task<PromotionStatisticsDto> BuildStatisticsAsync(Promotion promotion, long? fromDate, long? toDate)
        {
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.RepeatableRead);
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
            var to = toDate is > 0 ? DateTimeOffset.FromUnixTimeSeconds(toDate.Value).UtcDateTime : DateTime.UtcNow;
            var from = fromDate is > 0 ? DateTimeOffset.FromUnixTimeSeconds(fromDate.Value).UtcDateTime : to.AddDays(-30);
            query = query.Where(x => x.UsedAt >= from && x.UsedAt <= to);
            var totals = await AsyncExecuter.FirstOrDefaultAsync(query.GroupBy(x => 1).Select(g => new
            {
                Count = g.Count(), Discount = g.Sum(x => x.DiscountAmount), OrderAmount = g.Sum(x => x.OrderAmount),
                Revenue = g.Sum(x => x.FinalAmount), Paid = g.Sum(x => x.PaymentStatus == PaymentStatus.Paid ? x.FinalAmount : 0)
            }));
            var days = await AsyncExecuter.ToListAsync(query.GroupBy(x => (x.UsedAt + VietnamOffset).Date).OrderBy(g => g.Key)
                .Select(g => new { Date = g.Key, Count = g.Count(), Discount = g.Sum(x => x.DiscountAmount), Revenue = g.Sum(x => x.FinalAmount),
                    OrderAmount = g.Sum(x => x.OrderAmount), Paid = g.Sum(x => x.PaymentStatus == PaymentStatus.Paid ? x.FinalAmount : 0) }));
            await snapshot.CompleteAsync();

            return new PromotionStatisticsDto
            {
                GeneratedAt = DateTime.UtcNow, FromDate = from, ToDate = to,
                PromotionId = promotion.Id,
                Code = promotion.Code,
                UsedCount = totals?.Count ?? 0,
                TotalDiscountAmount = totals?.Discount ?? 0,
                TotalOrderAmount = totals?.OrderAmount ?? 0,
                Revenue = totals?.Revenue ?? 0,
                PaidRevenue = totals?.Paid ?? 0,
                Daily = days.Select(g => new PromotionDailyUsageDto
                    {
                        Date = g.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        UsedCount = g.Count,
                        DiscountAmount = g.Discount,
                        Revenue = g.Revenue,
                        TotalOrderAmount = g.OrderAmount,
                        PaidRevenue = g.Paid
                    })
                    .ToList()
            };
        }
        /// <summary>Statistics periods: at most 366 days; without dates the last 30 days up to now.</summary>
        public const int MaxStatisticsDays = 366;
        private static bool InvalidDates(long? from, long? to) => from is < 0 or > BaseFilterQuery.MaxUnixSeconds || to is < 0 or > BaseFilterQuery.MaxUnixSeconds || from.HasValue && to.HasValue && from > to ||
            (from is > 0 ? from.Value : (to is > 0 ? to.Value : DateTimeOffset.UtcNow.ToUnixTimeSeconds()) - 30L * 86400) < (to is > 0 ? to.Value : DateTimeOffset.UtcNow.ToUnixTimeSeconds()) - MaxStatisticsDays * 86400L;

        private static string? Validate(CreatePromotionCommand command, string code)
        {
            if (decimal.Round(command.DiscountValue, 2) != command.DiscountValue || decimal.Round(command.MinOrderAmount, 2) != command.MinOrderAmount ||
                command.DiscountValue > 9999999999999999m || command.MinOrderAmount > 9999999999999999m ||
                command.MaxDiscountAmount.HasValue && (decimal.Round(command.MaxDiscountAmount.Value, 2) != command.MaxDiscountAmount || command.MaxDiscountAmount > 9999999999999999m)) return "Số tiền tối đa 16 chữ số và hai chữ số thập phân.";
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
            Version = Version(x), CanDelete = x.UsedCount == 0,
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
        private static string Version(Promotion x) => AdministrativeWriteGuard.Version(new { x.Id, x.Code, x.Description, x.DiscountType, x.DiscountValue, x.MinOrderAmount, x.MaxDiscountAmount, StartAt = x.StartAt.Ticks / 10, EndAt = x.EndAt.Ticks / 10, x.UsageLimit, x.UsageLimitPerCustomer, x.UsedCount, x.IsActive, x.IsDeleted });
    }
}
