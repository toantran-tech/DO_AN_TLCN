using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Core.DataAccess.Repository
{
    public interface IMesRepository<TEntity, TKey> : IEfCoreRepository<TEntity, TKey> where TEntity : class, IEntity<TKey>
    {
        Task<(List<TDTO> data, long totalCount)> FilterProjectedDataAsync<TDTO>(
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? filterFunc,
            BaseFilterQuery parameters,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? defaultOrder = null
        ) where TDTO : class;

        Task<List<ColumnFilterDistinctValueDto>> GetColumnDistinctValuesAsync(
            string fieldName,
            BaseFilterQuery parameters,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? baseFilter = null,
            int maxValues = 200
        );
    }
}
