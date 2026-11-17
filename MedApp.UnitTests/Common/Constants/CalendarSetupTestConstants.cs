using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>
/// Synthetic data for the tests of what calendars are set up with: activity types, and staff's day templates and week
/// templates. Dates and times follow <see cref="CalendarTestConstants"/> ("today" is Monday 16 November 2026).
/// </summary>
public static class CalendarSetupTestConstants
{
    public const string ActivityTypeName = "Synthetic activity";
    public const string OtherActivityTypeName = "Other synthetic activity";
    public const string OtherWeekTemplateName = "Short week";

    /// <summary>A palette colour in mixed case with spaces, as a client might send it, and how it's stored.</summary>
    public const string UntidyColour = "  Teal ";
    public const string TidyColour = "teal";

    public const string NotAColour = "magenta";

    public const string TemplateNote = "Synthetic template note";

    /// <summary>An activity type that has been archived.</summary>
    public static CalendarActivityType CreateArchivedActivityType(string name = "Archived synthetic activity") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Colour = "gray",
        IsArchived = true
    };

    /// <summary>A copy of an activity type, so a test can change it without touching the shared one.</summary>
    public static CalendarActivityType Copy(CalendarActivityType activityType) => new()
    {
        Id = activityType.Id,
        Name = activityType.Name,
        Colour = activityType.Colour,
        IsArchived = activityType.IsArchived
    };

    /// <summary>A block of a day template as it's asked for.</summary>
    public static DayTemplateBlockValue BlockValue(
        int startHour,
        int endHour,
        Guid? activityTypeId = null,
        string? note = null,
        int startMinute = 0,
        int endMinute = 0) =>
        new(activityTypeId ?? Lunch.Id, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute), note);

    /// <summary>A day template (the clinic day by default, <see cref="StaffUserId"/>'s by default) with these blocks.</summary>
    public static DayTemplate BuildDayTemplate(
        IEnumerable<DayTemplateBlock>? blocks = null,
        Guid? id = null,
        string name = DayTemplateName,
        bool isArchived = false,
        Guid? ownerUserId = null) => new()
    {
        Id = id ?? DayTemplateId,
        OwnerUserId = ownerUserId ?? StaffUserId,
        Name = name,
        IsArchived = isArchived,
        Blocks = (blocks ?? []).ToList()
    };

    /// <summary>A week template (the standard week by default, <see cref="StaffUserId"/>'s by default) with these days.</summary>
    public static WeekTemplate BuildWeekTemplate(
        IEnumerable<WeekTemplateDay>? days = null,
        Guid? id = null,
        string name = CalendarTestConstants.WeekTemplateName,
        bool isArchived = false,
        Guid? ownerUserId = null) => new()
    {
        Id = id ?? WeekTemplateId,
        OwnerUserId = ownerUserId ?? StaffUserId,
        Name = name,
        IsArchived = isArchived,
        Days = (days ?? []).ToList()
    };

    /// <summary>A week template's day with its day template loaded, as the repository reads it.</summary>
    public static WeekTemplateDay CreateLoadedWeekDay(
        DayOfWeek dayOfWeek,
        DayTemplate dayTemplate,
        DateOnly? validFrom = null,
        DateOnly? validTo = null)
    {
        var day = CreateWeekDay(dayOfWeek, dayTemplate.Id, validFrom: validFrom, validTo: validTo);
        day.DayTemplate = dayTemplate;
        return day;
    }
}
