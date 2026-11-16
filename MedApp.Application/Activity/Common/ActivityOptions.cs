namespace MedApp.Application.Activity.Common;

/// <summary>The activity log's settings, from the <c>Activity</c> section of the API's configuration.</summary>
public sealed class ActivityOptions
{
    public const string Section = "Activity";

    /// <summary>
    /// How long a view is remembered: the same person opening the same patient (or procedure, or seeing them in their
    /// search results) again within this many minutes adds no new entry, so the log stays a readable size. 0 records
    /// every view.
    /// </summary>
    public int ViewWindowMinutes { get; set; } = 5;
}
