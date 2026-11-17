namespace MedApp.Application.Calendar.Common;

/// <summary>The lengths and counts meetings allow, as the database stores them.</summary>
public static class MeetingLimits
{
    public const int MaxTitleLength = 100;
    public const int MaxNotesLength = 500;
    public const int MaxAttendees = 100;

    /// <summary>The most people the team view and suggested times look at together.</summary>
    public const int MaxPeople = 100;

    public const string ChangedMeanwhile = "Someone else has changed this meeting since it was loaded. Reload it and try again.";
}
