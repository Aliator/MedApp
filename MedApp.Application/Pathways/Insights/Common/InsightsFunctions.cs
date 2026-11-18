using System.Text.Json;

namespace MedApp.Application.Pathways.Insights.Common;

/// <summary>
/// Functions the insights queries call that SQL has its own form of. Run in memory (the unit tests), the C# here runs;
/// in a query EF sends to SQL Server, Infrastructure maps each to SQL (<c>InsightsDbFunctions</c>).
/// </summary>
public static class InsightsFunctions
{
    /// <summary>Whole seconds from one moment to another (negative if <paramref name="to"/> is earlier). SQL: <c>DATEDIFF_BIG(second, from, to)</c>.</summary>
    public static long SecondsBetween(DateTime from, DateTime to) => (long)Math.Floor((to - from).TotalSeconds);

    /// <summary>
    /// The text at a simple JSON path (<c>$.a.b</c>) in a JSON document, or null if there's nothing there or it isn't
    /// JSON. SQL: <c>JSON_VALUE(json, path)</c> (lax), so the path must be a constant.
    /// </summary>
    public static string? JsonValue(string? json, string path)
    {
        if (string.IsNullOrEmpty(json) || !path.StartsWith("$."))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var element = document.RootElement;
            foreach (var name in path.Split('.').Skip(1))
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
                    return null;
            }

            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
