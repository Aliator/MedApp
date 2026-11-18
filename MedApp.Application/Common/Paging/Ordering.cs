using System.Linq.Expressions;

namespace MedApp.Application.Common.Paging;

/// <summary>
/// A list's order in either direction, for the grids' sortable columns (the column the person clicked, ascending or
/// descending). Plain <c>OrderBy</c> and <c>ThenBy</c>, so EF translates them; the list still ends on a unique key.
/// </summary>
public static class Ordering
{
    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> source, Expression<Func<T, TKey>> key, bool ascending) =>
        ascending ? source.OrderBy(key) : source.OrderByDescending(key);

    public static IOrderedQueryable<T> ThenByDirection<T, TKey>(
        this IOrderedQueryable<T> source, Expression<Func<T, TKey>> key, bool ascending) =>
        ascending ? source.ThenBy(key) : source.ThenByDescending(key);

    /// <summary>The column a request named, matched ignoring case, or null for one the list doesn't sort by.</summary>
    public static string? ColumnOf(string? sort, IReadOnlyCollection<string> columns) =>
        columns.FirstOrDefault(c => string.Equals(c, sort?.Trim(), StringComparison.OrdinalIgnoreCase));
}
