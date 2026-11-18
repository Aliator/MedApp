namespace MedApp.Application.Calendar.Common;

/// <summary>The lengths and counts calendars allow, as the database stores them.</summary>
public static class CalendarLimits
{
    public const int MaxActivityTypeNameLength = 60;
    public const int MaxTemplateNameLength = 100;

    /// <summary>A block's or template block's note.</summary>
    public const int MaxNoteLength = 200;

    public const int MaxDayTemplateBlocks = 30;

    /// <summary>A closed day's name ("Christmas Day").</summary>
    public const int MaxClosedDayNameLength = 100;
}
