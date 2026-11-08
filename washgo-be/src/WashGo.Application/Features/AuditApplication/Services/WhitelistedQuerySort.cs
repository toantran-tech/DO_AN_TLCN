using System.Linq.Expressions;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Features.AuditApplication.Services;

internal static class WhitelistedQuerySort
{
    public static bool IsValid(SortOption[]? options, IReadOnlyDictionary<string, string> fields) =>
        options == null || options.Length <= 8 && options.All(x => x != null && fields.ContainsKey(x.Field?.Trim() ?? ""));

    public static IOrderedQueryable<T> Apply<T>(IQueryable<T> query, SortOption[]? requested,
        IReadOnlyDictionary<string, string> fields, params SortOption[] defaults)
    {
        var sorts = requested is { Length: > 0 } ? requested : defaults;
        var ordered = false;
        foreach (var sort in sorts.Concat([new SortOption { Field = "Id" }]))
        {
            var parameter = Expression.Parameter(typeof(T), "row");
            var property = Expression.Property(parameter, fields[sort.Field.Trim()]);
            var key = Expression.Lambda(property, parameter);
            var method = ordered ? sort.IsDescending ? "ThenByDescending" : "ThenBy" : sort.IsDescending ? "OrderByDescending" : "OrderBy";
            query = query.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), method, [typeof(T), property.Type], query.Expression, Expression.Quote(key)));
            ordered = true;
        }
        return (IOrderedQueryable<T>)query;
    }
}
