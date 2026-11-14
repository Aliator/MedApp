namespace MedApp.Contracts.Common;

/// <summary>One page of a list, with the totals a client needs to show page controls.</summary>
/// <param name="Items">The rows on this page.</param>
/// <param name="Page">This page's number, from 1.</param>
/// <param name="PageSize">Rows per page.</param>
/// <param name="TotalCount">Rows matching the search, across every page.</param>
/// <param name="TotalPages">Pages in all; 0 when nothing matches.</param>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
