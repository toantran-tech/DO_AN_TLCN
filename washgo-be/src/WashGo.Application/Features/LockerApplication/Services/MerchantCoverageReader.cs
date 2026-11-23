using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Application.Contracts.Features.LockerContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.LockerApplication.Services
{
    /// <summary>
    /// One place that judges Merchants against a locker position with the service radius rule (MerchantCoverage).
    /// Used by locker writes, Merchant location/radius edits, and the Admin read APIs.
    /// </summary>
    public class MerchantCoverageReader : ITransientDependency
    {
        public const int MaxServiceRadiusKm = 100;

        private readonly IRepository<Merchant, Guid> _merchants;
        private readonly IRepository<User, Guid> _users;
        private readonly IRepository<Locker, Guid> _lockers;
        private readonly IRepository<LockerMerchant, Guid> _links;
        private readonly IAsyncQueryableExecuter _executer;

        public MerchantCoverageReader(IRepository<Merchant, Guid> merchants, IRepository<User, Guid> users,
            IRepository<Locker, Guid> lockers, IRepository<LockerMerchant, Guid> links, IAsyncQueryableExecuter executer)
        {
            _merchants = merchants;
            _users = users;
            _lockers = lockers;
            _links = links;
            _executer = executer;
        }

        /// <summary>
        /// Judges the given Merchants for a locker position. Merchants already linked (<paramref name="keptMerchantIds"/>)
        /// only need to be active and within the radius; newly added ones also need an active login and an unexpired contract.
        /// </summary>
        public async Task<List<EligibleMerchantDto>> EvaluateAsync(IReadOnlyCollection<Guid> merchantIds, double latitude, double longitude,
            IReadOnlyCollection<Guid> keptMerchantIds)
        {
            var ids = merchantIds.Distinct().ToList();
            var merchants = (await _merchants.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id);
            var activeAccounts = await ActiveAccountsAsync(ids);
            return ids.Select(id => merchants.TryGetValue(id, out var m)
                    ? Judge(m, latitude, longitude, activeAccounts.Contains(id), !keptMerchantIds.Contains(id))
                    : new EligibleMerchantDto { MerchantId = id, BlockedReasonCode = "MerchantNotFound" })
                .ToList();
        }

        /// <summary>Every Merchant that may newly take the locker position (active, valid login, contract, within radius).</summary>
        public async Task<List<EligibleMerchantDto>> EligibleAsync(double latitude, double longitude)
        {
            var merchants = await _merchants.GetListAsync(x => x.Status == MerchantStatus.Active);
            var activeAccounts = await ActiveAccountsAsync(merchants.Select(x => x.Id).ToList());
            return merchants.Select(m => Judge(m, latitude, longitude, activeAccounts.Contains(m.Id), true))
                .Where(x => x.Eligible).ToList();
        }

        /// <summary>
        /// Admin list for a locker position: eligible Merchants, plus every Merchant already linked to <paramref name="lockerId"/>
        /// (even when no longer eligible) and, with <paramref name="includeIneligible"/>, active Merchants outside the radius.
        /// Ordered eligible first, then by unrounded distance, name and id.
        /// </summary>
        public async Task<List<EligibleMerchantDto>> ListAsync(double latitude, double longitude, Guid? lockerId, string? keyword, bool includeIneligible)
        {
            var linked = lockerId.HasValue
                ? (await _links.GetListAsync(x => x.LockerId == lockerId.Value)).ToDictionary(x => x.MerchantId, x => x.Priority)
                : new Dictionary<Guid, int>();
            var linkedIds = linked.Keys.ToList();
            var query = (await _merchants.GetQueryableAsync()).Where(x => x.Status == MerchantStatus.Active || linkedIds.Contains(x.Id));
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim().ToLower();
                query = query.Where(x => x.BusinessName.ToLower().Contains(term) || x.Address.ToLower().Contains(term) || linkedIds.Contains(x.Id));
            }
            var merchants = await _executer.ToListAsync(query);
            var activeAccounts = await ActiveAccountsAsync(merchants.Select(x => x.Id).ToList());
            var rows = merchants.Select(m =>
            {
                var isLinked = linked.TryGetValue(m.Id, out var priority);
                var row = Judge(m, latitude, longitude, activeAccounts.Contains(m.Id), !isLinked);
                row.IsLinked = isLinked;
                row.LinkPriority = isLinked ? priority : null;
                return (Row: row, Exact: MerchantCoverage.DistanceKm(m, latitude, longitude));
            });
            return rows.Where(x => x.Row.Eligible || x.Row.IsLinked || includeIneligible)
                .OrderByDescending(x => x.Row.Eligible).ThenBy(x => x.Exact ?? double.MaxValue)
                .ThenBy(x => x.Row.BusinessName).ThenBy(x => x.Row.MerchantId)
                .Select(x => x.Row).ToList();
        }

        /// <summary>
        /// Linked lockers covered by the current Merchant position/radius that the proposed one would leave out, and the smallest
        /// whole radius (≤ 100 km) that would keep them at the proposed position.
        /// </summary>
        public async Task<(List<CoverageExcludedLockerDto> Lockers, int? SuggestedServiceRadiusKm)> ExcludedLockersAsync(
            Merchant current, decimal latitude, decimal longitude, int serviceRadiusKm)
        {
            if (current.Latitude == latitude && current.Longitude == longitude && current.ServiceRadiusKm == serviceRadiusKm)
                return ([], null);
            var links = await _links.GetQueryableAsync();
            var lockers = await _executer.ToListAsync((await _lockers.GetQueryableAsync())
                .Where(l => !l.IsDeleted && links.Any(x => x.LockerId == l.Id && x.MerchantId == current.Id)));
            var excluded = lockers
                .Where(l => MerchantCoverage.Covers(current, l.Latitude, l.Longitude) &&
                            !MerchantCoverage.Covers(latitude, longitude, serviceRadiusKm, l.Latitude, l.Longitude))
                .Select(l => (Locker: l, Exact: MerchantCoverage.DistanceKm(latitude, longitude, l.Latitude, l.Longitude)))
                .OrderBy(x => x.Locker.Code).ToList();
            int? suggested = excluded.Count == 0 || excluded.Any(x => !x.Exact.HasValue) ? null
                : (int)Math.Ceiling(excluded.Max(x => x.Exact!.Value)) is var need && need <= MaxServiceRadiusKm ? Math.Max(need, serviceRadiusKm) : null;
            return (excluded.Select(x => new CoverageExcludedLockerDto
            {
                LockerId = x.Locker.Id, Code = x.Locker.Code, Name = x.Locker.Name, ServiceRadiusKm = serviceRadiusKm,
                DistanceKm = x.Exact.HasValue ? MerchantCoverage.Display(x.Exact.Value) : null
            }).ToList(), suggested);
        }

        /// <summary>409 MerchantCoverageWouldExcludeLockers: nothing is saved; the Admin moves/removes the assignments first.</summary>
        public static BaseResponse<T> Blocked<T>(Guid merchantId, int serviceRadiusKm, List<CoverageExcludedLockerDto> lockers, int? suggested) => new()
        {
            Succeeded = false, StatusCode = HttpStatusCode.Conflict,
            Message = $"Thay đổi làm {lockers.Count} locker đang phụ trách nằm ngoài bán kính phục vụ: {string.Join(", ", lockers.Select(x => x.Code))}. Hãy chuyển/gỡ phân công các locker này trước.",
            Errors = new Dictionary<string, string[]> { ["serviceRadiusKm"] = ["Bán kính/vị trí mới không bao gồm locker đang phụ trách."] },
            Meta = new
            {
                code = MerchantCoverage.WouldExcludeLockers, fields = new[] { "serviceRadiusKm", "latitude", "longitude" },
                merchantId, serviceRadiusKm, suggestedServiceRadiusKm = suggested, lockers
            }
        };

        /// <summary>Stored links that fail the radius rule (legacy data or positions changed before the rule existed).</summary>
        public async Task<List<LockerCoverageIssueDto>> IssuesAsync()
        {
            var lockers = (await _lockers.GetListAsync(x => !x.IsDeleted)).ToDictionary(x => x.Id);
            var lockerIds = lockers.Keys.ToList();
            var links = await _links.GetListAsync(x => lockerIds.Contains(x.LockerId));
            var merchantIds = links.Select(x => x.MerchantId).Distinct().ToList();
            var merchants = (await _merchants.GetListAsync(x => merchantIds.Contains(x.Id))).ToDictionary(x => x.Id);
            bool Serves(LockerMerchant link) => merchants.TryGetValue(link.MerchantId, out var m) && m.Status == MerchantStatus.Active &&
                MerchantCoverage.Covers(m, lockers[link.LockerId].Latitude, lockers[link.LockerId].Longitude);
            var served = links.Where(Serves).Select(x => x.LockerId).ToHashSet();
            var issues = new List<LockerCoverageIssueDto>();
            foreach (var link in links)
            {
                if (!merchants.TryGetValue(link.MerchantId, out var m)) continue;
                var locker = lockers[link.LockerId];
                var problem = MerchantCoverage.LocationProblem(m, locker.Latitude, locker.Longitude);
                if (problem == null) continue;
                var distance = MerchantCoverage.DistanceKm(m, locker.Latitude, locker.Longitude);
                var needed = distance.HasValue ? (int)Math.Ceiling(distance.Value) : (int?)null;
                issues.Add(new LockerCoverageIssueDto
                {
                    LockerId = locker.Id, LockerCode = locker.Code, LockerName = locker.Name, LockerStatus = locker.Status,
                    MerchantId = m.Id, BusinessName = m.BusinessName, Priority = link.Priority,
                    DistanceKm = distance.HasValue ? MerchantCoverage.Display(distance.Value) : null,
                    ServiceRadiusKm = m.ServiceRadiusKm, ReasonCode = problem,
                    LockerStillServed = served.Contains(locker.Id),
                    SuggestedServiceRadiusKm = needed is >= 1 and <= MaxServiceRadiusKm ? Math.Max(1, needed.Value) : null
                });
            }
            return issues.OrderBy(x => x.LockerStillServed).ThenBy(x => x.LockerCode).ThenBy(x => x.Priority).ToList();
        }

        private async Task<HashSet<Guid>> ActiveAccountsAsync(List<Guid> ids) =>
            (await _executer.ToListAsync((await _users.GetQueryableAsync())
                .Where(u => ids.Contains(u.Id) && u.IsActive && !u.IsDeleted).Select(u => u.Id))).ToHashSet();

        private static EligibleMerchantDto Judge(Merchant m, double latitude, double longitude, bool accountActive, bool newLink)
        {
            var distance = MerchantCoverage.DistanceKm(m, latitude, longitude);
            var within = distance.HasValue && distance.Value <= m.ServiceRadiusKm;
            var reason = m.Status != MerchantStatus.Active ? "MerchantInactive"
                : MerchantCoverage.LocationProblem(m, latitude, longitude) is { } problem ? problem
                : newLink && !accountActive ? "AccountInactive"
                : newLink && MerchantContractRules.IsExpired(m.ContractEndDate, DateTime.UtcNow) ? "ContractExpired"
                : null;
            return new EligibleMerchantDto
            {
                MerchantId = m.Id, BusinessName = m.BusinessName, Address = m.Address, City = m.City, Status = m.Status,
                ServiceRadiusKm = m.ServiceRadiusKm, DistanceKm = distance.HasValue ? MerchantCoverage.Display(distance.Value) : null,
                WithinServiceRadius = within, Eligible = reason == null, BlockedReasonCode = reason
            };
        }
    }
}
