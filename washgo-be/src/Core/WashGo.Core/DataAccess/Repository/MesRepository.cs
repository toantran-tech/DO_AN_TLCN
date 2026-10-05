using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.ObjectMapping;
using WashGo.Core.Domain.Common;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Core.DataAccess.Repository
{
    public class MesRepository<TDbContext, TEntity, TKey> : EfCoreRepository<TDbContext, TEntity, TKey>, IMesRepository<TEntity, TKey>
        where TDbContext : IEfCoreDbContext
        where TEntity : class, IEntity<TKey>
    {
        protected readonly IObjectMapper _objectMapper;

        public MesRepository(IDbContextProvider<TDbContext> dbContextProvider, IObjectMapper objectMapper)
            : base(dbContextProvider)
        {
            _objectMapper = objectMapper;
        }

        public virtual async Task<(List<TDTO> data, long totalCount)> FilterProjectedDataAsync<TDTO>(
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? filterFunc,
            BaseFilterQuery parameters,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? defaultOrder = null
        ) where TDTO : class
        {
            var dbSet = await GetDbSetAsync();
            IQueryable<TEntity> query = dbSet.AsNoTracking();

            // Soft-delete filter
            if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
            {
                query = query.Where(e => !((ISoftDelete)e).IsDeleted);
            }

            // Apply base domain filter
            if (filterFunc != null)
            {
                query = filterFunc(query);
            }

            // Date range filter on CreatedOn
            if (parameters.FromDate > 0 && typeof(ICreatable).IsAssignableFrom(typeof(TEntity)))
            {
                query = query.Where(e => ((ICreatable)e).CreatedOn >= parameters.FromDate);
            }
            if (parameters.ToDate > 0 && typeof(ICreatable).IsAssignableFrom(typeof(TEntity)))
            {
                query = query.Where(e => ((ICreatable)e).CreatedOn <= parameters.ToDate);
            }

            // Column filters
            if (parameters.ColumnFilters != null && parameters.ColumnFilters.Count > 0)
            {
                foreach (var cf in parameters.ColumnFilters)
                {
                    if (string.IsNullOrWhiteSpace(cf.Field)) continue;
                    var prop = typeof(TEntity).GetProperty(cf.Field, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null) continue;

                    if (cf.SelectedValues != null && cf.SelectedValues.Count > 0)
                    {
                        var stringValues = cf.SelectedValues.Where(s => !string.IsNullOrEmpty(s)).ToList();
                        if (stringValues.Count > 0)
                        {
                            query = query.Where(e => stringValues.Contains(EF.Property<string>(e, prop.Name)));
                        }
                    }
                }
            }

            long totalCount = await query.LongCountAsync();

            // Ordering
            if (parameters.SortBy != null && parameters.SortBy.Length > 0)
            {
                var sort = parameters.SortBy[0];
                var prop = typeof(TEntity).GetProperty(sort.Field, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    query = sort.IsDescending
                        ? query.OrderByDescending(e => EF.Property<object>(e, prop.Name))
                        : query.OrderBy(e => EF.Property<object>(e, prop.Name));
                }
                else if (defaultOrder != null)
                {
                    query = defaultOrder(query);
                }
            }
            else if (defaultOrder != null)
            {
                query = defaultOrder(query);
            }

            // Paging (1-indexed or 0-indexed tolerance)
            int pageIndex = parameters.Page > 1 ? parameters.Page - 1 : 0;
            int take = parameters.Take > 0 ? parameters.Take : 20;

            var entities = await query.Skip(pageIndex * take).Take(take).ToListAsync();
            var dtos = _objectMapper.Map<List<TEntity>, List<TDTO>>(entities);

            return (dtos, totalCount);
        }

        public virtual async Task<List<ColumnFilterDistinctValueDto>> GetColumnDistinctValuesAsync(
            string fieldName,
            BaseFilterQuery parameters,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? baseFilter = null,
            int maxValues = 200
        )
        {
            var dbSet = await GetDbSetAsync();
            IQueryable<TEntity> query = dbSet.AsNoTracking();

            if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
            {
                query = query.Where(e => !((ISoftDelete)e).IsDeleted);
            }

            if (baseFilter != null)
            {
                query = baseFilter(query);
            }

            var prop = typeof(TEntity).GetProperty(fieldName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                return [];
            }

            var result = await query
                .GroupBy(e => EF.Property<string>(e, prop.Name))
                .Select(g => new ColumnFilterDistinctValueDto
                {
                    Value = g.Key,
                    Label = g.Key,
                    Count = g.LongCount()
                })
                .Where(x => x.Value != null)
                .OrderByDescending(x => x.Count)
                .Take(maxValues)
                .ToListAsync();

            return result;
        }
    }
}
