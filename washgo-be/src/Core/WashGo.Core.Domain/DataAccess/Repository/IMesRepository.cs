using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Core.Domain.DataAccess.Repository;

public interface IMesRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
{
    Task<(List<TDto> data, long totalCount)> FilterProjectedDataAsync<TDto>(
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? filterFunc,
        BaseFilterQuery parameters,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? defaultOrder = null
    ) where TDto : class;

    Task<List<ColumnFilterDistinctValueDto>> GetColumnDistinctValuesAsync(
        string fieldName,
        BaseFilterQuery parameters,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? baseFilter = null,
        int maxValues = 200
    );
}
