namespace MedApp.Application.Calendar.Common;

/// <summary>The lengths and counts meetings allow, as the database stores them.</summary>
public static class MeetingLimits
{
    public const int MaxTitleLength = 100;
    public const int MaxNotesLength = 500;
    public const int MaxAttendees = 100;

    /// <summary>The most people the team view and suggested times look at together.</summary>
    public const int MaxPeople = 100;

    public const string ChangedMeanwhile = "Someone else changed this meeting just now. Reload it and try again.";
}
