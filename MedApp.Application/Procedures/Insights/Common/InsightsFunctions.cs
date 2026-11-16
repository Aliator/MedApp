namespace MedApp.Application.Procedures.Insights.Common;

/// <summary>
/// Functions the insights queries call that SQL has its own form of. Run in memory (the unit tests), the C# here runs;
/// in a query EF sends to SQL Server, Infrastructure maps each to SQL (<c>InsightsDbFunctions</c>).
/// </summary>
public static class InsightsFunctions
{
    /// <summary>Whole seconds from one moment to another (negative if <paramref name="to"/> is earlier). SQL: <c>DATEDIFF_BIG(second, from, to)</c>.</summary>
    public static long SecondsBetween(DateTime from, DateTime to) => (long)Math.Floor((to - from).TotalSeconds);
}
