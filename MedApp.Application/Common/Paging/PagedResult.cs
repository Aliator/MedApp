namespace MedApp.Application.Common.Paging;

/// <summary>A page of results from a repository or service, and how many match in all.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
