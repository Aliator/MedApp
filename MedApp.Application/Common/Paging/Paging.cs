namespace MedApp.Application.Common.Paging;

public static class Paging
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    /// <summary>Rows to skip to reach a page (pages count from 1).</summary>
    public static int Skip(int page, int pageSize) => (page - 1) * pageSize;

    /// <summary>Pages needed for <paramref name="totalCount"/> rows; 0 when there are none.</summary>
    public static int TotalPages(int totalCount, int pageSize) =>
        totalCount <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
}
