using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.AuditContracts.Queries;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Application.Contracts.Features.AuditContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Audit;

namespace WashGo.Application.Features.AuditApplication.Services
{
    /// <summary>Read-only access to the audit trail; there is deliberately no update or delete.</summary>
    public class AuditLogAppService : WashGoAppService, IAuditLogAppService
    {
        private readonly IRepository<AuditLog, Guid> _repo;

        public AuditLogAppService(IRepository<AuditLog, Guid> repo) => _repo = repo;

        public async Task<PaginatedResult<AuditLogDto>> GetListAsync(AuditLogFilterQuery query)
        {
            var dateError = query.DateRangeError();
            if (dateError != null)
                return new PaginatedResult<AuditLogDto> { Succeeded = false, StatusCode = System.Net.HttpStatusCode.BadRequest, Message = dateError };
            var q = (await _repo.GetQueryableAsync()).AsQueryable();
            if (query.FromDate > 0)
            {
                var from = DateTime.UnixEpoch.AddSeconds(query.FromDate);
                q = q.Where(x => x.OccurredAt >= from);
            }
            if (query.ToDate > 0)
            {
                var to = DateTime.UnixEpoch.AddSeconds(query.ToDate + 1);
                q = q.Where(x => x.OccurredAt < to);
            }
            if (query.ActorId.HasValue) q = q.Where(x => x.ActorId == query.ActorId);
            if (!string.IsNullOrWhiteSpace(query.ActorRole)) q = q.Where(x => x.ActorRole == query.ActorRole.Trim());
            if (!string.IsNullOrWhiteSpace(query.Module)) q = q.Where(x => x.Module == query.Module.Trim());
            if (!string.IsNullOrWhiteSpace(query.Action)) q = q.Where(x => x.Action == query.Action.Trim());
            if (!string.IsNullOrWhiteSpace(query.ResourceType)) q = q.Where(x => x.ResourceType == query.ResourceType.Trim());
            if (!string.IsNullOrWhiteSpace(query.ResourceId)) q = q.Where(x => x.ResourceId == query.ResourceId.Trim());
            if (query.Result.HasValue) q = q.Where(x => x.Result == query.Result);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var k = query.Keyword.Trim().ToLower();
                q = q.Where(x => x.ActorName.ToLower().Contains(k) ||
                                 x.ResourceCode != null && x.ResourceCode.ToLower().Contains(k) ||
                                 x.Reason != null && x.Reason.ToLower().Contains(k));
            }
            return await PageAsync(q, query.Page, query.Take);
        }

        public async Task<BaseResponse<AuditLogDto>> GetByIdAsync(Guid id)
        {
            var row = await _repo.FindAsync(id);
            return row == null ? NotFound<AuditLogDto>("Không tìm thấy bản ghi nhật ký.") : Success(ToDto(row));
        }

        public async Task<PaginatedResult<AuditLogDto>> GetMyActivitiesAsync(MyActivityQuery query)
        {
            var me = CurrentUser.Id;
            var q = (await _repo.GetQueryableAsync()).Where(x => x.ActorId == me);
            if (!string.IsNullOrWhiteSpace(query.Module)) q = q.Where(x => x.Module == query.Module.Trim());
            return await PageAsync(q, query.Page ?? 1, query.Take ?? 20);
        }

        private async Task<PaginatedResult<AuditLogDto>> PageAsync(IQueryable<AuditLog> q, int page, int take)
        {
            page = Math.Max(1, page);
            take = Math.Clamp(take, 1, 100);
            var total = await AsyncExecuter.LongCountAsync(q);
            var rows = await AsyncExecuter.ToListAsync(q.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * take).Take(take));
            return Success(rows.Select(ToDto), total, page, take, total == 0 ? "Chưa có hoạt động nào." : null);
        }

        private static AuditLogDto ToDto(AuditLog x) => new()
        {
            Id = x.Id,
            OccurredAt = DateTime.SpecifyKind(x.OccurredAt, DateTimeKind.Utc),
            ActorId = x.ActorId,
            ActorName = x.ActorName,
            ActorRole = x.ActorRole,
            Module = x.Module,
            Action = x.Action,
            ResourceType = x.ResourceType,
            ResourceId = x.ResourceId,
            ResourceCode = x.ResourceCode,
            Result = x.Result,
            Reason = x.Reason,
            ErrorCode = x.ErrorCode,
            ChangedFields = string.IsNullOrEmpty(x.ChangedFields) ? [] : x.ChangedFields.Split(',').ToList(),
            Details = string.IsNullOrEmpty(x.Details) ? null : JsonDocument.Parse(x.Details).RootElement.Clone(),
            CorrelationId = x.CorrelationId
        };
    }
}
