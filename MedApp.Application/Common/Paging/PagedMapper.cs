using MedApp.Contracts.Common;

namespace MedApp.Application.Common.Paging;

/// <summary>Turns a page of results into the paged response every search endpoint returns.</summary>
public static class PagedMapper
{
    /// <summary>A page of results that are already responses.</summary>
    public static PagedResponse<T> ToResponse<T>(PagedResult<T> result, int page, int pageSize) =>
        new(result.Items, page, pageSize, result.TotalCount, Paging.TotalPages(result.TotalCount, pageSize));

    /// <summary>A page of results, each mapped to its response.</summary>
    public static PagedResponse<TResponse> ToResponse<TItem, TResponse>(
        PagedResult<TItem> result,
        int page,
        int pageSize,
        Func<TItem, TResponse> map) =>
        ToResponse(new PagedResult<TResponse>(result.Items.Select(map).ToList(), result.TotalCount), page, pageSize);
}
